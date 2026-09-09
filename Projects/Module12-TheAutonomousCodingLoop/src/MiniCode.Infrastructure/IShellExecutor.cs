namespace MiniCode.Infrastructure;

/// <summary>
/// Runs an external process to completion, or kills it. The only thing in
/// MiniCode that calls <see cref="System.Diagnostics.Process"/> directly.
/// </summary>
public interface IShellExecutor
{
    /// <summary>
    /// Runs <paramref name="command"/> with <paramref name="arguments"/> inside
    /// <paramref name="workingDirectory"/>, killing it if it outruns
    /// <paramref name="timeout"/>.
    /// </summary>
    Task<ShellCommandResult> RunAsync(
        string command,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
