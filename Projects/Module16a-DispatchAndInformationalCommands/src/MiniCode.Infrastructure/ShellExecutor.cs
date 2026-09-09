using System.Diagnostics;

namespace MiniCode.Infrastructure;

/// <summary>
/// The one place that starts a process. Arguments travel as a list, never a
/// concatenated command line, so there is no shell to escape and nothing for
/// an argument to break out of.
/// </summary>
public sealed class ShellExecutor : IShellExecutor
{
    /// <inheritdoc />
    public async Task<ShellCommandResult> RunAsync(
        string command,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(command)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            cancellationToken.ThrowIfCancellationRequested();
            return new ShellCommandResult(null, await stdout, await stderr, TimedOut: true);
        }

        return new ShellCommandResult(process.ExitCode, await stdout, await stderr, TimedOut: false);
    }
}
