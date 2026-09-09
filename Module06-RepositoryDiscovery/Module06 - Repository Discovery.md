# Module 6 — Repository Discovery

## Project Overview

We teach MiniCode to read a solution's *shape* before it reads any of its code — which projects exist, what they target, what they reference, and which ones are tests. It is the difference between an agent that opens files hoping to find something and one that knows where to look.

## Prerequisites

**Starting point:** open `Module05b-ReadingFiles/`.

Module 4's `IWorkspace` and Module 5b's `IFileSystemService`, plus Module 5a's `ToolCatalog` and the `Intercept` wrapper — the new tool is registered exactly like the others.

This is the last Module of Phase 1. It ships as part of **Phase 1 Capstone — Repository Explorer (v0.1)**.

## Setup

Nothing new. `System.Xml.Linq` is in the `net10.0` shared framework, so `MiniCode.Workspace` still has **zero** package references.

## Core Concepts

**An agent that starts by listing files is starting badly.** `list_files` on an unfamiliar repository returns a wall of paths with no structure — the model has to guess which are interesting, and it guesses by reading, which is expensive. A `.csproj` answers structural questions directly and costs one file read each.

**Project files are a map somebody already drew.** They name the projects, their target frameworks, which projects depend on which, and every NuGet package in play. That is most of what "describe this architecture" means, and none of it requires a model.

**Deterministic first, model second.** This is the pattern the whole course leans on: work out everything you can in C#, and spend the model on the part that genuinely needs judgement. Parsing XML is not judgement.

**We read XML; we do not evaluate MSBuild.** A real build resolves imports, conditions, and `$(Property)` expansion. We do none of that — a value written `$(LangVersion)` is reported as written. This is a deliberate limit, and the lesson names it rather than letting a viewer discover it later. For an agent orienting itself, "close enough to navigate by" beats "correct but requires the build engine."

**The sandbox does the filtering, again.** The inspector enumerates `.csproj` files and passes each through `IsPathAllowed`, so anything under `bin`, `obj` or `.git` never appears. Module 4's boundary has now done three jobs: gatekeeper for reads, filter for listings, and filter for discovery.

**A test project is worth calling out.** Knowing which projects are tests changes what the agent does with them — and it matters from Module 13 onward, where MiniCode runs tests and reads the results. One signal is enough here: a recognised test package in the references.

**Notable files are recognised, not parsed.** `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `README.md`, `appsettings.json` — their *presence* tells the model something (central package management is in play; there is a README worth reading). Opening them is `read_file`'s job, and the model can decide.

## The Code

### `src/MiniCode.Workspace/ProjectInfo.cs`

```csharp
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
```

### `src/MiniCode.Workspace/IRepositoryInspector.cs`

```csharp
namespace MiniCode.Workspace;

/// <summary>
/// Reads the repository's shape from its project files. Deterministic — it
/// parses XML rather than asking a model what it thinks is there.
/// </summary>
public interface IRepositoryInspector
{
    /// <summary>Every project the sandbox allows, in path order.</summary>
    IReadOnlyList<ProjectInfo> Inspect();
}
```

### `src/MiniCode.Workspace/RepositoryInspector.cs`

```csharp
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
```

### `src/MiniCode.Tools/RepositoryTools.cs`

```csharp
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
```

`ToolCatalog` gains another entry, through the same private `Intercept`:

```csharp
Intercept(AIFunctionFactory.Create(
    repositoryTools.DescribeRepository,
    name: "describe_repository",
    description: "Summarises the solution: its projects, target frameworks, project "
               + "references and NuGet packages. Call this first to learn the shape "
               + "of an unfamiliar repository.")),
```

## Walkthrough

1. **`ProjectInfo` is a record with five fields and one derived property.** No builder, no options, no result wrapper — the shape of a project is genuinely just this.
2. **`IsTestProject` lives on the record**, not in the parser, because it is a question about the data rather than about the file.
3. **`Inspect` reads like the sentence it implements** — enumerate, make relative, filter through the sandbox, sort, parse.
4. **`e.Name.LocalName` rather than `e.Name`.** Older `.csproj` files carry the MSBuild XML namespace and newer SDK-style ones do not; comparing the local name handles both without caring which you have.
5. **`TargetFrameworks` is split on `;` and de-duplicated**, so a multi-targeted project reports `net10.0, net8.0` and a single-targeted one reports one value — the caller never has to know which property was used.
6. **`ProjectReference` values are reduced to a project name.** The raw value is a relative path with backslashes; the model wants `MiniCode.Workspace`, not `..\MiniCode.Workspace\MiniCode.Workspace.csproj`.
7. **The description tells the model to call this first.** That single sentence is what changes its opening move from listing to orienting.

## Exercise

**One:** support the legacy `.sln` format. `RepositoryInspector` finds projects by scanning for `.csproj`, which works but ignores the solution file entirely. Parse `.sln` — the `Project("{GUID}") = "Name", "path\to\Name.csproj", "{GUID}"` lines — and report projects that are *in* the solution separately from stray ones that are not. Acceptance criteria: a project on disk but absent from the `.sln` is reported as such; solution folders (which have a different type GUID and no `.csproj` path) are skipped; a repository with no `.sln` behaves exactly as it does now.

**Two:** honour `Directory.Build.props` and `Directory.Packages.props`. A project that sets no `TargetFramework` may inherit one, and central package management moves versions out of the `.csproj` entirely. Find the nearest such file at or above each project, and merge what it declares. Acceptance criteria: a project with no `TargetFramework` of its own reports the inherited value and marks it as inherited; a `PackageReference` with no `Version` picks up its version from `Directory.Packages.props`; a repository with neither file behaves exactly as it does now.

Both are in `src/MiniCode.Workspace/RepositoryInspector.cs`. Neither has a reference implementation in a later Module folder — the criteria are the specification.

## Expected Output

Point MiniCode at its own folder and ask it to describe itself. This is the real output of `describe_repository` run against `Module06-RepositoryDiscovery`:

```text
Solutions: MiniCode.slnx
src/MiniCode.Agent/MiniCode.Agent.csproj
    targets   net10.0
    refs      MiniCode.Infrastructure, MiniCode.Tools, MiniCode.Workspace
    packages  Microsoft.Agents.AI, Microsoft.Extensions.AI.OpenAI
src/MiniCode.Cli/MiniCode.Cli.csproj
    targets   net10.0
    refs      MiniCode.Agent
    packages  none
src/MiniCode.Infrastructure/MiniCode.Infrastructure.csproj
    targets   net10.0
    refs      none
    packages  none
src/MiniCode.Tools/MiniCode.Tools.csproj
    targets   net10.0
    refs      MiniCode.Infrastructure, MiniCode.Workspace
    packages  Microsoft.Extensions.AI
src/MiniCode.Workspace/MiniCode.Workspace.csproj
    targets   net10.0
    refs      none
    packages  none
Notable files: none
```

Read that back against Module 3's dependency diagram — `Cli` references only `Agent`; `Workspace` and `Infrastructure` reference nothing. The architecture we drew three Modules ago is now something MiniCode can state on its own, from the files, without being told.

And in conversation:

```text
> dotnet run --project src/MiniCode.Cli -- D:\Repos\CustomerPortal

Workspace: D:\Repos\CustomerPortal
MiniCode. Type 'exit' to quit.

> Describe the architecture of this solution.

CustomerPortal is a three-project .NET 10 solution. CustomerPortal.Web is the
entry point and references Core and Data; Core holds the domain model and
references nothing; Data references Core and brings in EF Core and SQL Server.
CustomerPortal.Tests is a test project covering Core.

> exit
```
