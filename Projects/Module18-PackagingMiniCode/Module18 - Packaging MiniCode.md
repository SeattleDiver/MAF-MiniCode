# Module 18 — Packaging MiniCode

## Project Overview

Every prior Module ran MiniCode with `dotnet run --project src/MiniCode.Cli`. This Module turns it into something installable: `dotnet tool install -g MiniCode`, then `minicode` from inside any repository — no project path to remember, no source checkout required.

## Prerequisites

**Starting point:** open `Module17-Observability/`.

Nothing structural — this Module packages what already exists rather than building new capability. `Program.cs`'s existing `Directory.GetCurrentDirectory()` fallback, unchanged since Module 5a, is what makes running from an arbitrary directory already work.

Sixth and last Module of Phase 4. Ships as part of **Phase 4 Capstone — Developer CLI (v0.4)**.

## Setup

No new package. `MiniCode.Cli.csproj` gains a second `PropertyGroup` — `PackAsTool`, `ToolCommandName`, `PackageId`, `Version` — with nothing to install for it. One new, optional environment variable: `MINICODE_MODEL`. Set it to run against a different chat model; leave it unset and MiniCode still targets `gpt-4.1-mini`, exactly as every prior Module has.

## Core Concepts

**A .NET global tool is a console app with three MSBuild properties, not a different kind of project.** `MiniCode.Cli` has been a plain `Exe` since Module 3. `PackAsTool` and `ToolCommandName` are the only things that turn `dotnet pack` into something `dotnet tool install` understands — no code changes anywhere in the solution.

**The whole dependency graph packs for free.** `dotnet pack src/MiniCode.Cli` pulls in `MiniCode.Agent`, `MiniCode.Tools`, `MiniCode.Workspace` and `MiniCode.Infrastructure` the same way `dotnet run` already does — a tool package is built from the same output, not a separately-maintained bundle.

**Configuration still means exactly one type reading the environment.** `CodingAgentFactory` has been the only place in the solution that calls `Environment.GetEnvironmentVariable` since Module 3 — first for `OPENAI_API_KEY`, now for `MINICODE_MODEL` too. Two variables, one reader, same rule.

**The default is still the default.** `DefaultModelId` is still the literal `"gpt-4.1-mini"` this course has used throughout — `MINICODE_MODEL` is something an operator can set, not something MiniCode ever changes on its own.

**Two version numbers, kept in sync by convention, not by code.** `MiniCodeVersion.Current` is what the startup banner prints; `<Version>` in `MiniCode.Cli.csproj` is what `dotnet pack` stamps on the `.nupkg`. Nothing reads one from the other — they move together because a Phase Capstone is the only place either one is touched, the same discipline that already governs `MiniCodeVersion.Current` alone.

**Installed, MiniCode cannot tell it was ever run any other way.** There is no "am I a global tool" branch anywhere in the solution — `Environment.CurrentDirectory` means the same thing whether `dotnet run` started the process or the tool shim in `~/.dotnet/tools` did.

## The Code

### `src/MiniCode.Cli/MiniCode.Cli.csproj`

```xml
  <PropertyGroup>
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>minicode</ToolCommandName>
    <PackageId>MiniCode</PackageId>
    <Version>0.3.0</Version>
    <Description>A coding agent for the terminal, built on Microsoft Agent Framework.</Description>
  </PropertyGroup>
```

Model configuration, from `src/MiniCode.Agent/CodingAgentFactory.cs`:

```csharp
    private const string DefaultModelId = "gpt-4.1-mini";
    private const string ApiKeyVariable = "OPENAI_API_KEY";
    private const string ModelVariable = "MINICODE_MODEL";

    /// <summary>Builds a ready-to-use agent over the given workspace root.</summary>
    public static async Task<ICodingAgent> CreateAsync(
        string workspaceRoot,
        IApprovalPrompter prompter,
        CancellationToken cancellationToken = default)
    {
        string apiKey = Environment.GetEnvironmentVariable(ApiKeyVariable)
            ?? throw new InvalidOperationException(
                $"{ApiKeyVariable} is not set. Set it before running MiniCode.");
        string modelId = Environment.GetEnvironmentVariable(ModelVariable) is { Length: > 0 } configured
            ? configured
            : DefaultModelId;
```

`GetChatClient(ModelId)` becomes `GetChatClient(modelId)` — the one call site that used the old constant now uses the resolved variable instead. Nothing else in `CreateAsync` changes.

## Walkthrough

1. **`PackAsTool` requires an `Exe` output type and a `ToolCommandName`.** MiniCode already had the first; the second is the only new decision this Module makes — `minicode`, lowercase, matching the syllabus's own example exactly.
2. **`PackageId` is independent of `AssemblyName`.** The assembly is still `MiniCode.Cli.dll`; the installable package is `MiniCode`, and the command an operator types is `minicode` — three names, three separate properties, none inferred from the others.
3. **`Environment.GetEnvironmentVariable(ModelVariable) is { Length: > 0 } configured` treats an empty string the same as an unset variable.** Setting `MINICODE_MODEL=` in a shell should not silently ask OpenAI for a model named `""`.
4. **The `?? throw` for the API key and the `is {} ? :` for the model read the same way on purpose.** One is required and fails loudly; the other is optional and falls back quietly — the shape of each expression matches which behavior it is.
5. **No file in `MiniCode.Cli` other than the `.csproj` changed.** `Program.cs`'s workspace resolution, written for `dotnet run` and never touched since, is exactly what makes `cd CustomerPortal && minicode` work — packaging exposed that it was already correct rather than requiring it to become correct.

## Exercise

`minicode` currently has no way to ask what version is installed short of starting it and reading the banner. Add a `--version` flag, handled in `src/MiniCode.Cli/Program.cs` before the workspace is resolved: `minicode --version` prints `MiniCodeVersion.Current` and exits 0, without requiring `OPENAI_API_KEY` to be set at all. Acceptance criteria: `minicode --version` succeeds even with no key configured; running with any other arguments, or none, behaves exactly as it does today; the printed version matches `MiniCodeVersion.Current` exactly, with no `v` prefix, so it can be consumed by a script. No reference implementation ships in a later Module folder.

## Expected Output

All of the following is real: this Module's own `MiniCode.Cli` was packed, installed into an isolated tool path (not the machine's global tools — nothing from this capture is still installed), and run from a directory outside the repository entirely.

```text
> dotnet pack src/MiniCode.Cli -c Release -o ./nupkg
  MiniCode.Infrastructure -> ...\MiniCode.Infrastructure.dll
  MiniCode.Workspace -> ...\MiniCode.Workspace.dll
  MiniCode.Tools -> ...\MiniCode.Tools.dll
  MiniCode.Agent -> ...\MiniCode.Agent.dll
  MiniCode.Cli -> ...\MiniCode.Cli.dll
  MiniCode.Cli -> ...\publish\
  Successfully created package '...\nupkg\MiniCode.0.3.0.nupkg'.
```

```text
> dotnet tool install --tool-path ./toolpath --add-source ./nupkg MiniCode --version 0.3.0
You can invoke the tool using the following command: minicode
Tool 'minicode' (version '0.3.0') was successfully installed.
```

```text
> cd CustomerPortal
> minicode
```

```text
OPENAI_API_KEY is not set. Set it before running MiniCode.
```

And with a key set — the banner and workspace line, resolved from wherever `minicode` was actually run:

```text
MiniCode v0.3
Workspace: C:\Users\...\CustomerPortal
MiniCode. Type 'exit' to quit, or /help for commands.

>
```

The workspace line names `CustomerPortal`, not this course's own repository — the whole point of a global tool is that it never has to.
