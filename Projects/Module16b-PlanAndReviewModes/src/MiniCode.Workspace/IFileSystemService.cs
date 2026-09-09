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

    /// <summary>Creates a file, or overwrites one deliberately. Returns lines written.</summary>
    int WriteFile(string? path, string? content, bool overwrite = false);

    /// <summary>
    /// Replaces one unique anchor. Returns the 1-based line the edit landed on.
    /// Refuses when the anchor is missing, appears twice, or the file has moved on.
    /// </summary>
    int EditFile(string? path, string? find, string? replace, string? expectedFingerprint = null);

    /// <summary>Twelve hex characters identifying the file’s current bytes.</summary>
    string Fingerprint(string? path);

    /// <summary>Reads a text file, line-numbered so the model can cite locations.</summary>
    string ReadFile(string? path, int startLine = 1);
}
