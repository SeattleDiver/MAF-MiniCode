using System.Text;

namespace MiniCode.Workspace;

/// <summary>
/// The read side of the workspace. Refusals surface as exceptions carrying the
/// sandbox's reason — the tool layer turns those into text the model can act on.
/// </summary>
public sealed class FileSystemService(IWorkspace workspace) : IFileSystemService
{
    /// <summary>The most entries <see cref="ListFiles"/> will return.</summary>
    public const int MaxEntries = 200;
    private const int MaxLines = 400;

    /// <inheritdoc />
    public IReadOnlyList<string> ListFiles(string? path, bool recursive = false)
    {
        string directory = workspace.ResolvePath(string.IsNullOrWhiteSpace(path) ? "." : path);
        SearchOption depth = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        return [.. Directory.EnumerateFiles(directory, "*", depth)
            .Select(f => Path.GetRelativePath(workspace.Root, f).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(workspace.IsPathAllowed)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Take(MaxEntries)];
    }

    /// <inheritdoc />
    public string ReadFile(string? path, int startLine = 1)
    {
        string file = workspace.ResolvePath(path);
        string[] lines = File.ReadAllLines(file);
        int first = Math.Clamp(startLine, 1, Math.Max(lines.Length, 1));

        var text = new StringBuilder();
        foreach (int number in Enumerable.Range(first, Math.Min(MaxLines, lines.Length - first + 1)))
        {
            text.AppendLine($"{number,6}  {lines[number - 1]}");
        }

        int last = first + Math.Min(MaxLines, lines.Length - first + 1) - 1;
        if (last < lines.Length)
        {
            text.AppendLine($"... {lines.Length - last} more lines. Call again with startLine {last + 1}.");
        }

        return text.ToString();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> SearchFiles(string? query, string? path = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var hits = new List<string>();
        foreach (string file in ListFiles(path, recursive: true))
        {
            string[] lines = File.ReadAllLines(workspace.ResolvePath(file));
            for (int i = 0; i < lines.Length && hits.Count < MaxEntries; i++)
            {
                if (lines[i].Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    hits.Add($"{file}:{i + 1}: {lines[i].Trim()}");
                }
            }
        }

        return hits;
    }
}
