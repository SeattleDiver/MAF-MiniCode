using System.Xml.Linq;

namespace MiniCode.Workspace;

/// <summary>
/// Finds and parses every <c>.csproj</c> inside the workspace. It reads XML; it
/// does not evaluate MSBuild, so a value like <c>$(Version)</c> is reported as
/// written rather than resolved.
/// </summary>
public sealed class RepositoryInspector(IWorkspace workspace) : IRepositoryInspector
{
    /// <inheritdoc />
    public IReadOnlyList<ProjectInfo> Inspect() =>
        [.. Directory.EnumerateFiles(workspace.Root, "*.csproj", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(workspace.Root, f).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(workspace.IsPathAllowed)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(Read)];

    private ProjectInfo Read(string relativePath)
    {
        XDocument doc = XDocument.Load(workspace.ResolvePath(relativePath));

        return new ProjectInfo(
            Path.GetFileNameWithoutExtension(relativePath),
            relativePath,
            Frameworks(doc),
            [.. Items(doc, "ProjectReference").Select(p => Path.GetFileNameWithoutExtension(p) ?? p)],
            [.. Items(doc, "PackageReference")]);
    }

    private static IReadOnlyList<string> Frameworks(XDocument doc) =>
        [.. doc.Descendants()
            .Where(e => e.Name.LocalName is "TargetFramework" or "TargetFrameworks")
            .SelectMany(e => e.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct()];

    private static IEnumerable<string> Items(XDocument doc, string name) =>
        doc.Descendants()
            .Where(e => e.Name.LocalName == name)
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .Where(v => v.Length > 0);
}
