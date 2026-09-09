using MiniCode.Tools;
using MiniCode.Workspace;
using Xunit;

namespace MiniCode.Tests;

/// <summary>Verifies repository discovery. Ours, not the course's.</summary>
public sealed class RepositoryTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("minicode").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private IRepositoryInspector Build(string csproj, string relative = "src/App/App.csproj")
    {
        string full = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, csproj);
        return new RepositoryInspector(new MiniCode.Workspace.Workspace(_root));
    }

    [Fact]
    public void ReadsFrameworksReferencesAndPackages()
    {
        IRepositoryInspector sut = Build("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\Core\Core.csproj" />
                <PackageReference Include="Serilog" Version="4.0.0" />
              </ItemGroup>
            </Project>
            """);

        ProjectInfo p = Assert.Single(sut.Inspect());
        Assert.Equal("App", p.Name);
        Assert.Equal(["net10.0"], p.TargetFrameworks);
        Assert.Equal(["Core"], p.ProjectReferences);
        Assert.Equal(["Serilog"], p.PackageReferences);
        Assert.False(p.IsTestProject);
    }

    [Fact]
    public void SplitsMultipleTargetFrameworks()
    {
        IRepositoryInspector sut = Build("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFrameworks>net10.0;net8.0</TargetFrameworks></PropertyGroup>
            </Project>
            """);

        Assert.Equal(["net10.0", "net8.0"], Assert.Single(sut.Inspect()).TargetFrameworks);
    }

    [Fact]
    public void HandlesTheLegacyMsBuildNamespace()
    {
        IRepositoryInspector sut = Build("""
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);

        Assert.Equal(["net10.0"], Assert.Single(sut.Inspect()).TargetFrameworks);
    }

    [Fact]
    public void DetectsATestProject()
    {
        IRepositoryInspector sut = Build("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><PackageReference Include="xunit.v3" Version="4.0.0" /></ItemGroup>
            </Project>
            """);

        Assert.True(Assert.Single(sut.Inspect()).IsTestProject);
    }

    [Fact]
    public void SkipsProjectsInIgnoredDirectories()
    {
        IRepositoryInspector sut = Build("<Project Sdk=\"Microsoft.NET.Sdk\"></Project>", "obj/Generated/Gen.csproj");
        Assert.Empty(sut.Inspect());
    }

    [Fact]
    public void SaysSoWhenThereAreNoProjects()
    {
        var ws = new MiniCode.Workspace.Workspace(_root);
        var tools = new RepositoryTools(new RepositoryInspector(ws), new FileSystemService(ws));
        Assert.Contains("No .csproj files found", tools.DescribeRepository());
    }
}
