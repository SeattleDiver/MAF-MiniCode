using System.Security.Cryptography;
using System.Text;

namespace MiniCode.Workspace;

/// <summary>
/// Reading and writing the repository, inside the sandbox. Refusals surface as
/// exceptions carrying the reason — the tool layer turns those into text the model
/// can act on.
/// </summary>
public sealed class FileSystemService(IWorkspace workspace) : IFileSystemService
{
    /// <summary>The most entries <see cref="ListFiles"/> will return.</summary>
    public const int MaxEntries = 200;
    private const int MaxLines = 400;

    /// <inheritdoc />
    public string Fingerprint(string? path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(workspace.ResolvePath(path))))[..12].ToLowerInvariant();

    /// <inheritdoc />
    public int WriteFile(string? path, string? content, bool overwrite = false)
    {
        string file = workspace.ResolvePath(path);
        if (File.Exists(file) && !overwrite)
        {
            throw new InvalidOperationException(
                $"'{path}' already exists. Read it first, or pass overwrite: true to replace it.");
        }

        string text = content ?? string.Empty;
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, text);
        return text.Split('\n').Length;
    }

    /// <inheritdoc />
    public int EditFile(string? path, string? find, string? replace, string? expectedFingerprint = null)
    {
        string file = workspace.ResolvePath(path);

        if (expectedFingerprint is not null && Fingerprint(path) != expectedFingerprint)
        {
            throw new InvalidOperationException(
                $"'{path}' has changed since you read it (now {Fingerprint(path)}). Read it again.");
        }

        // A model writes line breaks however it likes, so find and replace are
        // converted to the file's own line endings before an exact match.
        string text = File.ReadAllText(file);
        string newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        string anchor = (find ?? string.Empty).ReplaceLineEndings(newline);
        replace = (replace ?? string.Empty).ReplaceLineEndings(newline);
        if (anchor.Trim().Length == 0)
        {
            throw new InvalidOperationException($"That text does not appear in '{path}'.");
        }

        int first = text.IndexOf(anchor, StringComparison.Ordinal);
        if (first < 0)
        {
            // Models often miscount indentation, so retry ignoring each line's
            // leading whitespace, and indent the replacement to match the file.
            (first, anchor, replace) = MatchIgnoringIndentation(path, text, anchor, replace, newline)
                ?? throw new InvalidOperationException(NotFoundMessage(path, text, anchor));
        }

        if (text.IndexOf(anchor, first + 1, StringComparison.Ordinal) >= 0)
        {
            throw new InvalidOperationException(
                $"That text appears more than once in '{path}'. Include enough context to make it unique.");
        }

        File.WriteAllText(file, string.Concat(text[..first], replace, text[(first + anchor.Length)..]));
        return text[..first].Count(c => c == '\n') + 1;
    }

    private static (int Start, string Matched, string Replacement)? MatchIgnoringIndentation(
        string? path, string text, string anchor, string replacement, string newline)
    {
        string[] lines = text.Split(newline);
        string[] wanted = anchor.TrimEnd('\r', '\n').Split(newline);
        bool endsWithNewline = anchor.EndsWith(newline, StringComparison.Ordinal);

        List<int> starts = [];
        for (int i = 0; i + wanted.Length <= lines.Length; i++)
        {
            if (wanted.Select((line, j) => line.Trim() == lines[i + j].Trim()).All(same => same)
                && (!endsWithNewline || i + wanted.Length < lines.Length))
            {
                starts.Add(i);
            }
        }

        if (starts.Count == 0)
        {
            return null;
        }

        if (starts.Count > 1)
        {
            throw new InvalidOperationException(
                $"That text appears more than once in '{path}'. Include enough context to make it unique.");
        }

        int start = starts[0];
        int sample = Array.FindIndex(wanted, line => line.Trim().Length > 0);
        string wantedIndent = Indent(wanted[sample]);
        string fileIndent = Indent(lines[start + sample]);
        string reindented = string.Join(newline, replacement.Split(newline).Select(line =>
            line.Length > 0 && line.StartsWith(wantedIndent, StringComparison.Ordinal)
                ? fileIndent + line[wantedIndent.Length..]
                : line));

        int offset = lines.Take(start).Sum(line => line.Length + newline.Length);
        string matched = string.Join(newline, lines.Skip(start).Take(wanted.Length)) + (endsWithNewline ? newline : "");
        return (offset, matched, reindented);
    }

    // The closest line by shared prefix, so a refused edit tells the model what
    // the file actually says instead of only that it was wrong.
    private static string NotFoundMessage(string? path, string text, string anchor)
    {
        string message = $"That text does not appear in '{path}'.";
        string wanted = anchor.Split('\n').Select(line => line.Trim()).First(line => line.Length > 0);
        string[] lines = text.Split('\n');

        int best = -1;
        int bestShared = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            int shared = 0;
            while (shared < line.Length && shared < wanted.Length && line[shared] == wanted[shared])
            {
                shared++;
            }

            if (shared > bestShared)
            {
                (best, bestShared) = (i, shared);
            }
        }

        return bestShared < Math.Min(wanted.Length, 8)
            ? message
            : $"{message} The closest line is {best + 1}: \"{lines[best].TrimEnd('\r')}\". "
              + "Copy the file's text exactly, indentation included.";
    }

    private static string Indent(string line) => line[..(line.Length - line.TrimStart().Length)];

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

        text.AppendLine($"Fingerprint {Fingerprint(path)} - pass this as expectedFingerprint when editing.");
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
