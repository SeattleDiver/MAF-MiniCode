namespace MiniCode.Agent;

/// <summary>
/// Asks a human before a tool call nobody has already approved for this
/// session. <c>MiniCode.Cli</c> is the only place this is ever answered from a
/// real console — everything else in the solution sees only this interface.
/// </summary>
public interface IApprovalPrompter
{
    /// <summary>Shows the pending call and returns what the operator decided.</summary>
    Task<ApprovalDecision> AskAsync(string description, CancellationToken cancellationToken);
}
