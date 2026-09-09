using System.Text;
using MiniCode.Workspace;

namespace MiniCode.Tools;

/// <summary>
/// Turns the repository's shape into the paragraph a model needs before it
/// starts opening files.
/// </summary>
public sealed class RepositoryTools(IRepositoryInspector inspector, IFileSystemService files)
{
    private static readonly string[] Notable =
        ["global.json", "Directory.Build.props", "Directory.Packages.props", "README.md", "appsettings.json"];

    /// <summary>Summarises solutions, projects, frameworks and dependencies.</summary>
    public string DescribeRepository()
    {
        IReadOnlyList<ProjectInfo> projects = inspector.Inspect();
        if (projects.Count == 0)
        {
            return "No .csproj files found in this workspace.";
        }

        IReadOnlyList<string> all = files.ListFiles(null, recursive: true);
        var text = new StringBuilder();

        text.AppendLine($"Solutions: {Join([.. all.Where(f => f.EndsWith(".slnx") || f.EndsWith(".sln"))])}");

        foreach (ProjectInfo p in projects)
        {
            text.AppendLine($"{p.RelativePath}{(p.IsTestProject ? " [test project]" : "")}");
            text.AppendLine($"    targets   {Join(p.TargetFrameworks)}");
            text.AppendLine($"    refs      {Join(p.ProjectReferences)}");
            text.AppendLine($"    packages  {Join(p.PackageReferences)}");
        }

        text.AppendLine($"Notable files: {Join([.. all.Where(f => Notable.Contains(Path.GetFileName(f), StringComparer.OrdinalIgnoreCase))])}");
        return text.ToString();
    }

    private static string Join(IReadOnlyList<string> values) => values.Count == 0 ? "none" : string.Join(", ", values);
}
