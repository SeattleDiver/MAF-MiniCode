using MiniCode.Agent;

namespace MiniCode.Cli;

/// <summary>
/// Answers an approval prompt on the real console. This is the only place in
/// the solution that reads a Y/N/A answer from a person.
/// </summary>
internal sealed class ConsoleApprovalPrompter : IApprovalPrompter
{
    /// <inheritdoc />
    public Task<ApprovalDecision> AskAsync(string description, CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine("MiniCode wants to run:");
        Console.WriteLine(description);
        Console.Write("Allow? [Y] Yes  [N] No  [A] Always allow for this session: ");

        ApprovalDecision decision = Console.ReadLine()?.Trim().ToUpperInvariant() switch
        {
            "A" => ApprovalDecision.AlwaysApprove,
            "Y" => ApprovalDecision.Approve,
            _ => ApprovalDecision.Deny,
        };

        return Task.FromResult(decision);
    }
}
