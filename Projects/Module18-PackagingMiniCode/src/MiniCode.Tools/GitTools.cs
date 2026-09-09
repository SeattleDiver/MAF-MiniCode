using System.ComponentModel;
using MiniCode.Infrastructure;
using MiniCode.Workspace;

namespace MiniCode.Tools;

/// <summary>
/// The model-facing side of Git. <see cref="IGitClient"/> decides how a query
/// runs; this decides what the model gets told about it.
/// </summary>
public sealed class GitTools(IGitClient git, IWorkspace workspace)
{
    /// <summary>Reports modified, added and untracked files, or that there are none.</summary>
    public async Task<string> GitStatus(CancellationToken cancellationToken = default) =>
        Format(await git.StatusAsync(workspace.Root, cancellationToken), "Working tree clean.");

    /// <summary>Reports unstaged changes, or that there are none.</summary>
    public async Task<string> GitDiff(CancellationToken cancellationToken = default) =>
        Format(await git.DiffAsync(workspace.Root, cancellationToken), "No unstaged changes.");

    /// <summary>Reports the most recent commits, or that there are none.</summary>
    public async Task<string> GitLog(
        [Description("How many recent commits to show.")] int maxEntries = 10,
        CancellationToken cancellationToken = default) =>
        Format(await git.LogAsync(workspace.Root, maxEntries, cancellationToken), "No commits yet.");

    private static string Format(ShellCommandResult result, string whenEmpty) =>
        result.ExitCode != 0
            ? result.StandardError.Trim()
            : string.IsNullOrWhiteSpace(result.StandardOutput) ? whenEmpty : result.StandardOutput.TrimEnd();
}
