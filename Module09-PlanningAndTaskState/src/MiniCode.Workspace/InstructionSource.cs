namespace MiniCode.Workspace;

/// <summary>
/// Reads <c>AGENTS.md</c> from the workspace root. The content is capped: it is
/// prepended to every request, so an enormous file would quietly eat the context
/// budget on every turn.
/// </summary>
public sealed class InstructionSource(IWorkspace workspace) : IInstructionSource
{
    /// <summary>The file MiniCode looks for.</summary>
    public const string FileName = "AGENTS.md";

    /// <summary>The most lines that will be read from it.</summary>
    public const int MaxLines = 200;

    /// <inheritdoc />
    public string? Load()
    {
        if (!workspace.IsPathAllowed(FileName))
        {
            return null;
        }

        string path = workspace.ResolvePath(FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        string[] lines = File.ReadAllLines(path);
        return lines.Length <= MaxLines
            ? string.Join(Environment.NewLine, lines)
            : string.Join(Environment.NewLine, lines.Take(MaxLines))
              + $"{Environment.NewLine}... truncated at {MaxLines} lines.";
    }
}
