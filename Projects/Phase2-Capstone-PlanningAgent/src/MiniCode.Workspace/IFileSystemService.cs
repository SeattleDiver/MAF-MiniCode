namespace MiniCode.Workspace;

/// <summary>
/// Reading the repository, inside the sandbox. Every path goes through
/// <see cref="IWorkspace"/> first, so there is no way to reach a file the
/// boundary would refuse.
/// </summary>
public interface IFileSystemService
{
    /// <summary>
    /// Lists files under a workspace-relative directory, as relative paths.
    /// Ignored directories are skipped rather than reported.
    /// </summary>
    IReadOnlyList<string> ListFiles(string? path, bool recursive = false);

    /// <summary>Finds lines matching a query, as <c>path:line: text</c>.</summary>
    IReadOnlyList<string> SearchFiles(string? query, string? path = null);

    /// <summary>Reads a text file, line-numbered so the model can cite locations.</summary>
    string ReadFile(string? path, int startLine = 1);
}
