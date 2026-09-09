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
    private readonly LoopGuardInvoker _guard;
    private int _usedTokens;

    internal CodingAgent(
        AIAgent agent,
        AgentSession session,
        IChatClient chatClient,
        string instructions,
        Planner planner,
        LoopGuardInvoker guard)
    {
        _agent = agent;
        _session = session;
        _chatClient = chatClient;
        _planner = planner;
        _guard = guard;
        _budget = ContextBudget.Default;
        _usedTokens = TokenEstimator.Estimate(instructions);
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
            yield return fragment;
        }

        if (_budget.ShouldCompact(_usedTokens))
        {
            _usedTokens -= SessionCompactor.Compact(_session);
        }
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

    public void Dispose() => _chatClient.Dispose();
}
