using System.ComponentModel;
using MiniCode.Workspace;

namespace MiniCode.Tools;

/// <summary>
/// The model-facing side of reading the repository. These methods decide what
/// the model sees; <see cref="IFileSystemService"/> decides what is legal.
/// </summary>
public sealed class FileTools(IFileSystemService files)
{
    /// <summary>Lists files, or says plainly that there are none.</summary>
    public string ListFiles(
        [Description("Directory relative to the workspace root. Omit for the root itself.")] string? path = null,
        [Description("Include subdirectories.")] bool recursive = false)
    {
        IReadOnlyList<string> found = files.ListFiles(path, recursive);
        if (found.Count == 0)
        {
            return $"No files under '{path ?? "."}'.";
        }

        string listing = string.Join(Environment.NewLine, found);
        return found.Count < FileSystemService.MaxEntries
            ? listing
            : listing + Environment.NewLine
              + $"... {FileSystemService.MaxEntries} entries shown. List a subdirectory to narrow the results.";
    }

    /// <summary>Finds matching lines, or says plainly that there are none.</summary>
    public string SearchFiles(
        [Description("Text to look for. Case-insensitive.")] string query,
        [Description("Directory to search under. Omit for the whole workspace.")] string? path = null)
    {
        IReadOnlyList<string> hits = files.SearchFiles(query, path);
        return hits.Count == 0
            ? $"No matches for '{query}'."
            : string.Join(Environment.NewLine, hits)
              + (hits.Count < FileSystemService.MaxEntries ? ""
                 : Environment.NewLine + $"... {FileSystemService.MaxEntries} matches shown. Narrow the search.");
    }

    /// <summary>Creates a file, refusing to clobber one by accident.</summary>
    public string WriteFile(
        [Description("File path relative to the workspace root.")] string path,
        [Description("The complete new contents of the file.")] string content,
        [Description("Set true only when you intend to replace an existing file.")] bool overwrite = false)
    {
        int lines = files.WriteFile(path, content, overwrite);
        return $"Wrote {path} ({lines} lines). Fingerprint {files.Fingerprint(path)}.";
    }

    /// <summary>Replaces one unique piece of text, or explains why it could not.</summary>
    public string EditFile(
        [Description("File path relative to the workspace root.")] string path,
        [Description("Exact text to replace. Include enough context to appear only once.")] string find,
        [Description("Text to put in its place.")] string replace,
        [Description("Fingerprint from the last read of this file.")] string? expectedFingerprint = null)
    {
        int line = files.EditFile(path, find, replace, expectedFingerprint);
        return $"Edited {path} at line {line}. Fingerprint {files.Fingerprint(path)}.";
    }

    /// <summary>Reads a file, line-numbered.</summary>
    public string ReadFile(
        [Description("File path relative to the workspace root.")] string path,
        [Description("First line to return. Use this to page through a long file.")] int startLine = 1)
        => files.ReadFile(path, startLine);
}
