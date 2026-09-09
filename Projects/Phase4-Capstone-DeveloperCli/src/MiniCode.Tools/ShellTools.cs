using System.ComponentModel;
using System.Text;
using MiniCode.Infrastructure;
using MiniCode.Workspace;

namespace MiniCode.Tools;

/// <summary>
/// The model-facing side of running a command. <see cref="IShellExecutor"/>
/// decides how a process runs; this decides what the model gets told about it.
/// </summary>
public sealed class ShellTools(IShellExecutor shell, IWorkspace workspace)
{
    /// <summary>The most characters of stdout or stderr handed back in one call.</summary>
    public const int MaxOutputLength = 4000;

    /// <summary>Runs a command in the workspace root and reports what happened.</summary>
    public async Task<string> RunCommand(
        [Description("The program to run, e.g. \"dotnet\". Not passed through a shell.")] string command,
        [Description("Arguments to pass, e.g. [\"build\"]. Omit for none.")] string[]? arguments = null,
        [Description("Seconds to allow before the process is killed.")] int timeoutSeconds = 120,
        CancellationToken cancellationToken = default)
    {
        string[] args = arguments ?? [];
        ShellCommandResult result = await shell.RunAsync(
            command, args, workspace.Root, TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);

        if (result.TimedOut)
        {
            return $"'{command}' did not finish within {timeoutSeconds}s and was killed.";
        }

        var text = new StringBuilder($"Exit code {result.ExitCode}");
        string? summary = IsTestRun(command, args) ? TestOutputParser.Summarize(result.StandardOutput) : null;
        if (!string.IsNullOrWhiteSpace(summary))
        {
            // A run that never reached the test host — a compile error — has no
            // summary to extract; the raw output is the only useful thing to show.
            text.Append(Environment.NewLine).Append(summary);
        }
        else
        {
            Append(text, result.StandardOutput);
        }

        Append(text, result.StandardError, "stderr:");
        return text.ToString();
    }

    private static bool IsTestRun(string command, string[] arguments) =>
        command.Equals("dotnet", StringComparison.OrdinalIgnoreCase)
        && arguments is [var sub, ..] && sub.Equals("test", StringComparison.OrdinalIgnoreCase);

    private static void Append(StringBuilder text, string output, string? label = null)
    {
        if (output.Length == 0)
        {
            return;
        }

        string capped = output.Length <= MaxOutputLength
            ? output
            : output[..MaxOutputLength] + $"{Environment.NewLine}... {output.Length - MaxOutputLength} more characters truncated.";

        text.Append(Environment.NewLine);
        if (label is not null)
        {
            text.Append(label).Append(Environment.NewLine);
        }

        text.Append(capped.TrimEnd());
    }
}
