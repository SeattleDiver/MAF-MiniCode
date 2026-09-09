# Phase 3 Capstone — Autonomous Fixer (v0.3)

## Project Overview

Phase 3 gave MiniCode hands: it can edit files, run a shell command, loop until a build or test run passes, and read what the test runner actually said. This Capstone stamps that as **v0.3**, and fixes three seams that only show up once the Phase is looked at as a whole — one dating back to Module 10, one to Module 11a, and one all the way back to Module 5a.

## Prerequisites

**Starting point:** open `Module13-AutomatedTestingAndSelfCorrection/`.

All of Phase 3 — safe file editing (Module 10), running a process safely and the command allow list (Module 11a/11b), the autonomous coding loop (Module 12), and automated testing and self-correction (Module 13) — plus everything Phase 1 and Phase 2 built underneath it.

## Setup

Nothing new. No package changes.

## Core Concepts

**A Phase is where the interactions surface.** Every Module in Phase 3 was verified in isolation and every one worked. The three defects below only exist *between* Modules — none of them was visible from inside the Module that introduced the code, only once a later Module changed what that code actually faces.

**Seam one: the "no changes" notice stopped being true the moment it mattered.** `Planner.NoChangesNotice` has read "No files have been modified." since Module 9, when it was accurate — nothing in the solution could modify a file yet. Module 10 gave MiniCode `edit_file` and `write_file`, and Module 12 put those tools in a loop that runs immediately after the plan is shown. The notice kept printing, now describing a run that was about to make it false within seconds. The fix is one word: "modified **yet**." A worded guarantee has an expiry date set by whatever Module ships next, not by the Module that wrote it.

**Seam two: compaction forgot half of what the agent had tried.** Module 8b's `WorkingState` distills a compacted run into what must survive — but it only ever watched file-shaped tool calls. Module 11a added `run_command`, a tool with no `path` argument at all, so `FileNote` never saw it. An agent mid-autonomous-loop that had already run `dotnet build` twice would lose both attempts the moment the session compacted, and could re-run a command it had already tried. `WorkingNoteKind` gains a `CommandRun` note kind, and `WorkingState` now records one for every `run_command` call.

**Seam three: the context budget can't see the loop it was built for.** Module 8a's whole premise is that every path that spends the window reports it. `CodingAgent.RunStreamingAsync` charges `TokenEstimator.Estimate(update.ToString())` for each streamed update — but `AgentResponseUpdate.ToString()` only concatenates `TextContent`. A tool call and its result carry no `TextContent` at all, so `.ToString()` silently returns an empty string for both. That was a rounding error before Module 12 existed to call up to twenty tools in one turn, some of them `run_command` results carrying thousands of characters of build or test output. The budget now also charges the serialized call arguments and the result text for every `FunctionCallContent` and `FunctionResultContent` in the update.

**What v0.3 is.** MiniCode can now read a repository, plan against it, edit a file, run a command from a fixed allow list, and keep looping build-then-test attempts until they pass or a guard trips it — all while a compaction preserves both the files it touched and the commands it already tried, and a context estimate that finally counts what those commands cost.

**What v0.3 is not.** Nobody is asked permission for any of it. Every edit lands, every command runs, every loop iteration fires, with no pause for a human to say yes. That is deliberate — Module 14 gives MiniCode Git, and Module 15 is what finally asks. The plan shown at the start of a turn is also still just a snapshot: its per-step markers stay `[ ]` even after the loop finishes the step, because nothing in Phase 3 writes back to a `TaskPlan` once planning is done. That gap predates this Phase — Module 9 built the state machine, nothing has driven it since — and Module 16 is where it gets a real driver.

## The Code

### `src/MiniCode.Cli/MiniCodeVersion.cs`

```csharp
namespace MiniCode.Cli;

/// <summary>
/// The version MiniCode reports at startup. It moves at a Phase Capstone and
/// nowhere else: 0.1 was the read-only Repository Explorer, 0.2 was the Planning
/// Agent, 0.3 is the Autonomous Fixer that closes Phase 3. It lives in the CLI
/// because versioning is a packaging concern, and packaging is where Module 18
/// picks this constant up.
/// </summary>
internal static class MiniCodeVersion
{
    public const string Current = "0.3";
}
```

Seam one — the notice is worded for what is true at the moment it prints, from `src/MiniCode.Agent/Planner.cs`:

```csharp
    /// <summary>
    /// The line MiniCode prints under every plan. Worded for what is true at
    /// the moment it prints, not for the run that follows immediately after —
    /// since Module 10, that run can modify files within seconds of this notice.
    /// </summary>
    public const string NoChangesNotice = "No files have been modified yet.";
```

Seam two — a command MiniCode already ran becomes a note, from `src/MiniCode.Agent/WorkingNoteKind.cs`:

```csharp
namespace MiniCode.Agent;

/// <summary>What a working note is about. These are the things that must survive
/// compaction — everything else in the conversation is expendable.</summary>
public enum WorkingNoteKind
{
    /// <summary>Something the operator asked for. Kept until it is clearly done.</summary>
    OpenTask,

    /// <summary>A conclusion MiniCode reached, including any decision it explained.</summary>
    CompletedWork,

    /// <summary>A file MiniCode has already looked at.</summary>
    FileState,

    /// <summary>A command MiniCode has already run.</summary>
    CommandRun,
}
```

And the note itself, from `src/MiniCode.Agent/WorkingState.cs`:

```csharp
            foreach (FunctionCallContent call in message.Contents.OfType<FunctionCallContent>())
            {
                Add(notes, WorkingNoteKind.FileState, FileNote(call));
                Add(notes, WorkingNoteKind.CommandRun, CommandNote(call));
            }
```

```csharp
    // Module 11a added a tool with no path at all, so FileNote never sees it —
    // a build or test run left no trace here before this note existed, and a
    // compaction mid-loop would forget the agent had tried it.
    private static string? CommandNote(FunctionCallContent call) =>
        call.Name != "run_command" ? null : JsonSerializer.Serialize(call.Arguments);
```

Seam three — the budget charges what `.ToString()` drops, from `src/MiniCode.Agent/CodingAgent.cs`:

```csharp
            string fragment = update.ToString();
            _usedTokens += TokenEstimator.Estimate(fragment) + TokenEstimator.Estimate(ToolActivityText(update));
            yield return fragment;
```

```csharp
    // update.ToString() concatenates only TextContent — a tool call and its result
    // carry none, so every one of Module 12's tool calls went uncharged here since
    // Module 5a first gave the agent a tool at all.
    private static string? ToolActivityText(AgentResponseUpdate update) =>
        string.Concat(update.Contents.Select(content => content switch
        {
            FunctionCallContent call => JsonSerializer.Serialize(call.Arguments),
            FunctionResultContent result => result.Result?.ToString(),
            _ => null,
        }));
```

## Walkthrough

1. **Seam one is a single word.** `NoChangesNotice` still prints in the same place, at the same moment — it now claims only what is true at that moment, not a guarantee about the turn that is about to run.
2. **Seam two follows Module 11a's own tool name.** `CommandNote` checks `call.Name != "run_command"` the same way `FileNote` checks for `write_file`/`edit_file` — a new tool gets a new recognizer, not a change to the old one.
3. **The same serialization Module 12 uses to compare tool calls is what describes one here** — `JsonSerializer.Serialize(call.Arguments)`, not `.ToString()`, because `AIFunctionArguments.ToString()` on an array-valued argument prints the type name, not its contents.
4. **`Label` grew one more case** so `CommandRun` notes render under their own heading, "Commands already run:", alongside the existing three sections.
5. **Seam three's fix lives entirely inside the `await foreach` loop.** `ToolActivityText` inspects the same `update.Contents` the loop already has; it adds no new call, no new type, and no new dependency.
6. **`FunctionResultContent.Result` is charged with a plain `.ToString()`**, not `JsonSerializer.Serialize` — a tool result is already the string MiniCode's tools return (see Module 5a and Module 13's `TestOutputParser`), not a value that needs serializing.
7. **Five files differ from `Module13-AutomatedTestingAndSelfCorrection/`** — `MiniCodeVersion.cs`, `Planner.cs`, `WorkingNoteKind.cs`, `WorkingState.cs`, `CodingAgent.cs` — and forty-one are carried forward byte-for-byte, out of forty-six `.cs` files total.

## Exercise

**One:** correlate a command's outcome, not just its shape. `WorkingState`'s new `CommandNote` records what was run but not whether it succeeded — a note reading `{"command":"dotnet","arguments":["test"]}` looks identical whether that run passed or failed. Use the `FunctionResultContent`'s matching `CallId` to find the tool result that followed the call, and fold its exit code into the note (e.g. `dotnet test — exit 0`). Acceptance criteria: a passing command's note ends differently from a failing one; a `run_command` call whose result has not arrived yet (still in flight) renders without a status rather than throwing; every other note kind is unaffected.

**Two:** make the plan reflect what actually happened. `TaskPlan`'s `WithState`/`Current`/`IsComplete` exist since Module 9 and nothing calls them during an autonomous run. After `RunStreamingAsync` completes, mark a plan step complete when its title contains "build" or "test" and the most recent matching `run_command` in the session history returned `Exit code 0`. Acceptance criteria: a plan whose build step ran clean renders `[x]` for that step; a step whose last matching command failed stays `[ ]`; steps with no matching command are untouched; the plan is re-rendered once, after the run, below the answer. Module 16 replaces this with a real driver — favour something you would be happy to delete.

Neither has a reference implementation in a later Module folder.

## Expected Output

The following is real output, captured by calling this Capstone's own `MiniCode.Agent` code directly — no model call involved, since all three seams are deterministic.

Seam one:

```text
No files have been modified yet.
```

Seam two, `WorkingState` across a build → fail → edit → build → pass run, then compacted:

```text
SESSION STATE (earlier turns were compacted away)

What was asked:
- Fix the build.

What was established:
- Fixed the missing semicolon; the build now passes.

Files already examined:
- src/Program.cs (modified)

Commands already run:
- {"command":"dotnet","arguments":["build"]}
```

Seam three, the same `AgentResponseUpdate` shapes `RunStreamingAsync` sees mid-loop, before and after the fix:

```text
callUpdate.ToString()      = []
ToolActivityText(call)     = [{"command":"dotnet","arguments":["test"]}]
resultUpdate.ToString()    = []
ToolActivityText(result)   = [Exit code 0
All tests passed.]
```

Before this Capstone, both of those `[]` lines were the entirety of what the budget saw for a tool call and its result — zero tokens charged for either, no matter how large the command or its output.

A full autonomous run — plan a fix, edit a file, run the build, run the tests, report back — needs a live `OPENAI_API_KEY`, since it is the model deciding what to search for, what to change, and when to stop looping, not this lesson's code. What it produces is Module 13's failing-test detection composed with Module 10's `edit_file` and Module 12's loop, exactly as Module 13's own lesson describes: a condensed `Exit code 2` failure points at a file and line, `read_file` or `search_files` finds the method, `edit_file` corrects it, and `run_command` runs the suite again to confirm — `Exit code 0` where it was `2` — with the difference that this Capstone's `WorkingState` now has a record of every one of those commands if the session compacts mid-loop, and its context estimate now reflects what each of them actually cost.
