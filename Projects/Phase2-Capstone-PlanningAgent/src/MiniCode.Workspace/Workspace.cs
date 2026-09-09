namespace MiniCode.Workspace;

/// <summary>
/// The sandbox. It is pure path arithmetic — nothing here touches the disk — so
/// the question "can the agent reach this file?" is answered by one small class.
/// </summary>
public sealed class Workspace : IWorkspace
{
    private static readonly string[] IgnoredDirectories = [".git", "bin", "obj", "node_modules"];

    private static readonly StringComparison Comparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly string _rootWithSeparator;

    public Workspace(string root)
    {
        Root = Path.GetFullPath(root);
        _rootWithSeparator = Root.EndsWith(Path.DirectorySeparatorChar)
            ? Root : Root + Path.DirectorySeparatorChar;
    }

    /// <inheritdoc />
    public string Root { get; }

    /// <inheritdoc />
    public PathValidationResult ValidatePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return PathValidationResult.Refuse(PathRejectionReason.Empty);
        }

        // Refuse rooted input before combining. Path.Combine silently discards
        // the root when its second argument is rooted, so "\Windows\System32"
        // would otherwise escape without ever looking like traversal.
        if (Path.IsPathRooted(path) || path.Contains(':'))
        {
            return PathValidationResult.Refuse(PathRejectionReason.NotRelative);
        }

        string fullPath;
        try
        {
            // GetFullPath is what collapses ".." and normalizes separators.
            fullPath = Path.GetFullPath(Path.Combine(Root, path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return PathValidationResult.Refuse(PathRejectionReason.Malformed);
        }

        // Compare against the root *with* its trailing separator, so a sibling
        // that merely shares the prefix — C:\repo-evil next to C:\repo — fails.
        if (!fullPath.StartsWith(_rootWithSeparator, Comparison) && !fullPath.Equals(Root, Comparison))
        {
            return PathValidationResult.Refuse(PathRejectionReason.OutsideWorkspace);
        }

        string relative = Path.GetRelativePath(Root, fullPath);
        string[] segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(s => IgnoredDirectories.Contains(s, StringComparer.FromComparison(Comparison)))
            ? PathValidationResult.Refuse(PathRejectionReason.IgnoredDirectory)
            : PathValidationResult.Allow(fullPath);
    }

    /// <inheritdoc />
    public string ResolvePath(string? path)
    {
        PathValidationResult result = ValidatePath(path);
        return result.IsAllowed
            ? result.FullPath
            : throw new InvalidOperationException($"Path '{path}' was refused: {result.Reason}.");
    }

    /// <inheritdoc />
    public bool IsPathAllowed(string? path) => ValidatePath(path).IsAllowed;
}
