# Phase 1 Capstone — Repository Explorer (v0.1)

## Project Overview

Phase 1 is finished, and MiniCode can now be pointed at a .NET repository it has never seen and asked about it. This Capstone stamps that state as **v0.1**, tightens the seams between the six Modules that built it, and is honest about the one thing it deliberately cannot do: change anything.

## Prerequisites

**Starting point:** open `Module06-RepositoryDiscovery/`.

All of Phase 1 — the agent and session (Module 2), the five-project layout (3), the sandbox (4), the interception seam and read tools (5a, 5b), and repository discovery (6).

## Setup

Nothing new. No package changes, and `MiniCode.Workspace` still has zero package references.

## Core Concepts

**A Capstone consolidates; it does not accumulate.** Module 6 already contained every project. This folder adds one small file, changes two, and spends the rest of its time proving the whole thing works together. If a Capstone needs to introduce a concept, something went wrong earlier.

**Version numbers move at Capstones and nowhere else.** A Module is a step; a Capstone is a state worth naming. `v0.1` is *Repository Explorer* — MiniCode reads and explains, and does nothing else. The number will move four more times, and each time it will mean the tool crossed a line worth marking.

**Five tools is a system, not a list.** `workspace_root` says where we are. `describe_repository` says what the solution is. `search_files` and `list_files` find candidates. `read_file` reads the few that matter. Each one's description points at the next, which is how the model learns the order without being given a script.

**Search before read is a budget decision, not a style preference.** Reading a repository into the context window costs tokens the model then has less room to think with. Orient, narrow, read a little: that discipline is why the instructions name the order explicitly, and Module 8 will build the machinery to enforce it.

**The sandbox has quietly done four jobs.** It gate-keeps reads, filters listings, filters discovery, and refuses with a reason a model can act on. It is 78 lines and has not changed since Module 4 — which is the argument for putting a boundary in before the capability that needs it.

**A silent truncation is worse than a refusal.** A refused call tells the model to try something else. A silently shortened one tells it nothing, and it reasons confidently from a partial answer. Both read tools now say when they have held something back — which they did not, until this Capstone was written and the claim was checked. That is what a consolidation pass is for.

**What v0.1 cannot do, deliberately.** It cannot write a file, run a command, use Git, plan, remember anything across a restart, or ask permission — because it never needs permission for anything it can currently do. Phase 2 adds project instructions, context management and planning; Phase 3 is where it first changes something on your disk.

## The Code

### `src/MiniCode.Cli/MiniCodeVersion.cs`

```csharp
namespace MiniCode.Cli;

/// <summary>
/// The version MiniCode reports at startup. It moves at a Phase Capstone and
/// nowhere else: 0.1 is the read-only Repository Explorer that closes Phase 1.
/// It lives in the CLI because versioning is a packaging concern, and packaging
/// is where Module 18 picks this constant up.
/// </summary>
internal static class MiniCodeVersion
{
    public const string Current = "0.1";
}
```

### `src/MiniCode.Cli/Program.cs`

```csharp
// -----------------------------------------------------------------------------
// MiniCode - the terminal front end
//
// The CLI resolves which directory MiniCode is allowed to work in and hands it
// to the agent layer. It still references no AI package and no tool type.
// -----------------------------------------------------------------------------

using MiniCode.Agent;

namespace MiniCode.Cli;

/// <summary>Entry point. It resolves the workspace, builds an agent, runs the loop.</summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        string root = Path.GetFullPath(args.Length > 0 ? args[0] : Directory.GetCurrentDirectory());

        if (!Directory.Exists(root))
        {
            Console.Error.WriteLine($"Workspace '{root}' does not exist.");
            return 1;
        }

        try
        {
            ICodingAgent agent = await CodingAgentFactory.CreateAsync(root);
            Console.WriteLine($"MiniCode v{MiniCodeVersion.Current}");
            Console.WriteLine($"Workspace: {root}");
            await new ConsoleChatLoop(agent).RunAsync();
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
```

The agent's standing instructions grow from one generic sentence into the order Phase 1's four tools are meant to be used in. From `src/MiniCode.Agent/CodingAgentFactory.cs`:

```csharp
    private const string Instructions =
        "You are MiniCode, a concise assistant for software developers. You answer "
        + "questions about the repository you are working in, using tools rather than "
        + "guessing. Call describe_repository first on an unfamiliar solution, then "
        + "list_files to locate what you need, then read_file on the few files that "
        + "matter. Paths are always relative to the workspace root. Cite files by path "
        + "and line number. If a tool refuses a call, read the message and correct the "
        + "request rather than retrying it unchanged.";
```

## Walkthrough

1. **`MiniCodeVersion` is one constant in one file.** Spelled out in three places, a version string disagrees with itself in two of them. Module 18 packages the tool and reads this same constant.
2. **The version prints above the workspace line**, so the first two lines of every session say what is running and where. The banner beneath them is fixed for the whole series and never advertises capabilities.
3. **The instructions now name the tool order.** Nothing in the tool layer changed to achieve that — the model's behaviour is being shaped by a paragraph, which is the cheapest lever available and the first one to reach for.
4. **"Correct the request rather than retrying it unchanged"** is aimed squarely at the refusal path. A sandbox rejection carries a reason; that sentence tells the model the reason is worth reading.
5. **Nothing else in the solution changed.** One file added, two changed, twenty-four carried forward byte-for-byte from Module 6 — which is the claim a Capstone exists to be able to make.

## Exercise

**One:** report the tool count at startup. Add a line under the workspace path that says how many tools the agent has — `5 tools available.` — sourced from the catalog rather than a hard-coded number, so it stays true as Phase 2 and 3 add more. It belongs in `src/MiniCode.Cli/`, and the interesting part is that `MiniCode.Cli` cannot see `AITool`: you will need `ICodingAgent` to expose a count, which is a small, honest widening of the boundary rather than a hole in it.

**Two:** add a `--version` argument. `minicode --version` should print the version and exit 0 without contacting OpenAI at all. Acceptance criteria: it works with no `OPENAI_API_KEY` set; it does not create an agent; any other argument is still treated as the workspace path. This matters more than it looks — Module 18 ships MiniCode as a global tool, and a tool that cannot state its own version without credentials is a nuisance.

## Expected Output

Pointed at its own folder, MiniCode v0.1 describes itself. The tool output below is real, captured from this solution:

```text
> dotnet run --project src/MiniCode.Cli -- D:\MAF-SmartAgent\Phase1-Capstone-RepositoryExplorer

MiniCode v0.1
Workspace: D:\MAF-SmartAgent\Phase1-Capstone-RepositoryExplorer
MiniCode. Type 'exit' to quit.

> What is this solution, and where would I start reading?

It's a five-project .NET 10 solution called MiniCode. MiniCode.Cli is the entry
point and references only MiniCode.Agent; MiniCode.Agent brings in the Microsoft
Agent Framework and OpenAI packages and references Tools, Workspace and
Infrastructure; MiniCode.Tools holds the model-facing tools; MiniCode.Workspace
and MiniCode.Infrastructure reference nothing internal.

Start at src/MiniCode.Cli/Program.cs:17, which resolves the workspace directory
and hands it to CodingAgentFactory.

> exit
```

Two behaviours worth showing on camera, both captured verbatim.

Reading past the end of a file clamps rather than fails, and the line numbers make the shortfall obvious:

```text
     9  internal static class MiniCodeVersion
    10  {
    11      public const string Current = "0.1";
    12  }
```

And the sandbox refuses with a reason the model can act on rather than an error it can only repeat:

```text
read_file failed: Path '../secrets.txt' was refused: OutsideWorkspace.
```

That is v0.1: it can read a repository it has never seen, explain the architecture, cite a file and a line, and it cannot touch anything. Phase 2 gives it a memory and a plan.
