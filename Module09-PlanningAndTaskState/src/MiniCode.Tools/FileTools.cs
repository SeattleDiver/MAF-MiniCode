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

    /// <summary>Reads a file, line-numbered.</summary>
    public string ReadFile(
        [Description("File path relative to the workspace root.")] string path,
        [Description("First line to return. Use this to page through a long file.")] int startLine = 1)
        => files.ReadFile(path, startLine);
}
