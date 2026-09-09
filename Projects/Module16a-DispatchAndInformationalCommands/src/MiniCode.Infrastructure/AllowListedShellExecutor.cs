namespace MiniCode.Infrastructure;

/// <summary>
/// Refuses a command before <see cref="IShellExecutor"/> ever sees it. The
/// same wrap-don't-touch shape as Module 5a's <c>InterceptedFunction</c>, one
/// layer lower: this decorates <see cref="IShellExecutor"/> instead of an
/// <c>AIFunction</c>, and the inner executor stays unaware it is guarded.
/// </summary>
public sealed class AllowListedShellExecutor(IShellExecutor inner, ICommandAllowList allowList) : IShellExecutor
{
    /// <inheritdoc />
    public async Task<ShellCommandResult> RunAsync(
        string command,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!allowList.IsAllowed(command, arguments))
        {
            string requested = $"{command} {string.Join(' ', arguments)}".TrimEnd();
            throw new InvalidOperationException(
                $"'{requested}' is not on the allow list. Allowed: {string.Join(", ", allowList.Entries)}.");
        }

        return await inner.RunAsync(command, arguments, workingDirectory, timeout, cancellationToken);
    }
}
