namespace MiniCode.Infrastructure;

/// <summary>
/// The three read-only Git queries MiniCode can make. There is deliberately no
/// method here that changes anything — Module 15 is what adds permission, and
/// there is nothing yet that would need to ask for it.
/// </summary>
public interface IGitClient
{
    /// <summary>Modified, added and untracked files against the current branch.</summary>
    Task<ShellCommandResult> StatusAsync(string workspaceRoot, CancellationToken cancellationToken);

    /// <summary>Unstaged changes to tracked files, in unified diff format.</summary>
    Task<ShellCommandResult> DiffAsync(string workspaceRoot, CancellationToken cancellationToken);

    /// <summary>The most recent commits, newest first.</summary>
    Task<ShellCommandResult> LogAsync(string workspaceRoot, int maxEntries, CancellationToken cancellationToken);
}
