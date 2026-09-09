namespace MiniCode.Workspace;

/// <summary>One project in the repository, as read from its <c>.csproj</c>.</summary>
public sealed record ProjectInfo(
    string Name,
    string RelativePath,
    IReadOnlyList<string> TargetFrameworks,
    IReadOnlyList<string> ProjectReferences,
    IReadOnlyList<string> PackageReferences)
{
    private static readonly string[] TestPackages = ["xunit", "nunit", "mstest", "microsoft.net.test.sdk"];

    /// <summary>True when a recognised test package is referenced.</summary>
    public bool IsTestProject => PackageReferences.Any(
        p => TestPackages.Any(t => p.StartsWith(t, StringComparison.OrdinalIgnoreCase)));
}
