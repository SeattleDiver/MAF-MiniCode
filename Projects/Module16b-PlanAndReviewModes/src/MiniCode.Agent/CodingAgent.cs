using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace MiniCode.Agent;

/// <summary>
/// MiniCode's single agent. It owns the MAF <see cref="AIAgent"/> and the
/// <see cref="AgentSession"/> that carries conversation history — owning the
/// session here is what lets Module 8 rewrite that history for long sessions.
/// </summary>
public sealed class CodingAgent : ICodingAgent, IDisposable
{
    private readonly AIAgent _agent;
    private readonly AgentSession _session;
    private readonly IChatClient _chatClient;
    private readonly ContextBudget _budget;
    private readonly Planner _planner;
    private readonly Reviewer _reviewer;
    private readonly LoopGuardInvoker _guard;
    private readonly IReadOnlyDictionary<string, AIFunction> _tools;
    private readonly int _baselineTokens;
    private int _usedTokens;

    internal CodingAgent(
        AIAgent agent,
        AgentSession session,
        IChatClient chatClient,
        string instructions,
        Planner planner,
        Reviewer reviewer,
        LoopGuardInvoker guard,
        IReadOnlyList<AITool> tools)
    {
        _agent = agent;
        _session = session;
        _chatClient = chatClient;
        _planner = planner;
        _reviewer = reviewer;
        _guard = guard;
        _budget = ContextBudget.Default;
        _baselineTokens = TokenEstimator.Estimate(instructions);
        _usedTokens = _baselineTokens;
        _tools = tools.OfType<AIFunction>().ToDictionary(t => t.Name);
    }

    /// <inheritdoc />
    public async Task<TaskPlan> PlanAsync(string request, CancellationToken cancellationToken = default)
    {
        TaskPlan plan = await _planner.CreateAsync(request, cancellationToken);

        // Planning is a model call like any other. Module 8a's budget is only
        // honest if every call it does not see is charged to it explicitly.
        _usedTokens += TokenEstimator.Estimate(request) + TokenEstimator.Estimate(plan.Render());
        return plan;
    }

    /// <inheritdoc />
    public string ContextSummary => _budget.Describe(_usedTokens);

    /// <inheritdoc />
    public async IAsyncEnumerable<string> RunStreamingAsync(
        string request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _guard.Reset();
        _usedTokens += TokenEstimator.Estimate(request);

        await foreach (AgentResponseUpdate update in
            _agent.RunStreamingAsync(request, _session, cancellationToken: cancellationToken))
        {
            string fragment = update.ToString();
            _usedTokens += TokenEstimator.Estimate(fragment) + TokenEstimator.Estimate(ToolActivityText(update));

            // A tool-only update has no TextContent for ToString() to show, so the
            // operator would otherwise watch nothing happen for as long as the call
            // takes. Show the call itself in its place.
            string display = fragment.Length > 0 ? fragment : DescribeToolCalls(update);
            if (display.Length > 0)
            {
                yield return display;
            }
        }

        if (_budget.ShouldCompact(_usedTokens))
        {
            _usedTokens -= SessionCompactor.Compact(_session);
        }
    }

    /// <inheritdoc />
    public async Task<string> InvokeToolAsync(
        string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        if (!_tools.TryGetValue(toolName, out AIFunction? function))
        {
            return $"No such tool: {toolName}.";
        }

        _guard.Reset();
        var boundArguments = new AIFunctionArguments();
        foreach ((string key, object? value) in arguments)
        {
            boundArguments[key] = value;
        }

        object? result = await function.InvokeAsync(boundArguments, cancellationToken);
        return result?.ToString() ?? string.Empty;
    }

    /// <inheritdoc />
    public void ClearSession()
    {
        _session.SetInMemoryChatHistory([]);
        _usedTokens = _baselineTokens;
    }

    /// <inheritdoc />
    public async Task<string> ReviewAsync(CancellationToken cancellationToken = default)
    {
        string diff = await InvokeToolAsync("git_diff", new Dictionary<string, object?>(), cancellationToken);
        string report = await _reviewer.ReviewAsync(diff, cancellationToken);

        // Review is a model call like any other — see PlanAsync above.
        _usedTokens += TokenEstimator.Estimate(diff) + TokenEstimator.Estimate(report);
        return report;
    }

    // update.ToString() concatenates only TextContent — a tool call and its result
    // carry none, so every one of Module 12's tool calls went uncharged here since
    // Module 5a first gave the agent a tool at all.
    private static string? ToolActivityText(AgentResponseUpdate update) =>
        string.Concat(update.Contents.Select(content => content switch
        {
            FunctionCallContent call => JsonSerializer.Serialize(call.Arguments),
            FunctionResultContent result => result.Result?.ToString(),
            _ => null,
        }));

    private static string DescribeToolCalls(AgentResponseUpdate update) =>
        string.Concat(update.Contents.OfType<FunctionCallContent>()
            .Select(call => $"{Environment.NewLine}→ {call.Name}({JsonSerializer.Serialize(call.Arguments)}){Environment.NewLine}"));

    public void Dispose() => _chatClient.Dispose();
}
