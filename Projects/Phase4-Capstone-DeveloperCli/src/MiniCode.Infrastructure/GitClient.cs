namespace MiniCode.Infrastructure;

/// <summary>
/// Git integration is just Module 11a's process runner pointed at a fixed
/// binary: every method here is one hardcoded <c>git</c> invocation, so there
/// is nothing here for a command allow list to guard.
/// </summary>
public sealed class GitClient(IShellExecutor shell) : IGitClient
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public Task<ShellCommandResult> StatusAsync(string workspaceRoot, CancellationToken cancellationToken) =>
        shell.RunAsync("git", ["status", "--porcelain"], workspaceRoot, Timeout, cancellationToken);

    /// <inheritdoc />
    public Task<ShellCommandResult> DiffAsync(string workspaceRoot, CancellationToken cancellationToken) =>
        shell.RunAsync("git", ["diff"], workspaceRoot, Timeout, cancellationToken);

    /// <inheritdoc />
    public Task<ShellCommandResult> LogAsync(string workspaceRoot, int maxEntries, CancellationToken cancellationToken) =>
        shell.RunAsync("git", ["log", "--oneline", "-n", maxEntries.ToString()], workspaceRoot, Timeout, cancellationToken);
}
