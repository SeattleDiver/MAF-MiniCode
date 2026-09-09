namespace MiniCode.Agent;

/// <summary>
/// The one thing the terminal is allowed to know about the agent. Deliberately
/// expressed in <see cref="string"/> only, so no framework type crosses into
/// <c>MiniCode.Cli</c>.
/// </summary>
public interface ICodingAgent
{
    /// <summary>What the session has spent so far, as a line for the operator.</summary>
    string ContextSummary { get; }

    /// <summary>Turns a request into an inspectable plan. Reads nothing, changes nothing.</summary>
    Task<TaskPlan> PlanAsync(string request, CancellationToken cancellationToken = default);

    /// <summary>Streams the agent's answer to a request, fragment by fragment.</summary>
    IAsyncEnumerable<string> RunStreamingAsync(
        string request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs one named tool directly, through the same approval and loop-guard
    /// policy the model's own calls pass through. For a slash command, not chat.
    /// </summary>
    Task<string> InvokeToolAsync(
        string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default);

    /// <summary>Discards the conversation so far. The workspace itself is untouched.</summary>
    void ClearSession();
}
