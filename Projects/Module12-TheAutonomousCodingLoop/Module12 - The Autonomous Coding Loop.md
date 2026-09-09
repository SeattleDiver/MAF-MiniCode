# Module 12 — The Autonomous Coding Loop

## Project Overview

Every tool MiniCode has — build, search, read, edit, build again — has existed since Module 11b. Until now a human has driven each step by hand. This Module is what lets the model chain them itself, inside a single request, and a guard that keeps that chain from spinning if it never converges.

## Prerequisites

**Starting point:** open `Module11b-TheCommandAllowList/`.

Module 5a's `IToolInvoker` seam and Module 11b's `AllowListedShellExecutor` — this Module reuses that exact wrap-don't-touch shape a second time. Nothing about `IShellExecutor`, `ShellTools`, or the allow list changes.

First Module of the back half of Phase 3. Ships as part of **Phase 3 Capstone — Autonomous Fixer (v0.3)**.

## Setup

Nothing new. `System.Text.Json` is in the shared framework.

## Core Concepts

```text
User: "Fix the build."
     │
     ▼
CodingAgent ── run dotnet build ── examine errors ── search ── read ── edit
     ▲                                                                  │
     └──────────────────── run dotnet build again ◄─────────────────────┘
                                      │
                         FAILED ──────┴────── SUCCESS
                         (continue)          (stop)
```

**MAF already loops tool calls inside one request — every Module since 5a has been relying on this without needing to look at it.** `ChatClientAgent` wraps the model in a `FunctionInvokingChatClient`: the model asks for a tool, MAF runs it, feeds the result back, and asks the model again, repeating until it stops asking for tools. One call to `RunStreamingAsync` can already contain the whole build-fix-rebuild sequence — this Module is the first time that loop needs a policy of MiniCode's own.

**That loop already has its own settings, and MiniCode still doesn't touch them.** `FunctionInvokingChatClient` exposes `MaximumIterationsPerRequest` (default 40) and `MaximumConsecutiveErrorsPerRequest` (default 3), and the instance `ChatClientAgent` builds internally is reachable — `agent.GetService<FunctionInvokingChatClient>()` returns it, and its properties are settable. MiniCode leaves both alone: 40 sits comfortably above the cap this Module adds, so it stays a distant, silent backstop that should never fire; and `MaximumConsecutiveErrorsPerRequest` counts thrown exceptions, which Module 5a's never-throw rule means MiniCode's tools never produce — a build that fails is a normal, successful call returning `Exit code 1`, not an exception.

**So the guard belongs exactly where Module 5a already put the seam.** Wrap `IToolInvoker` — the same shape as Module 11b's `AllowListedShellExecutor`, one seam over. `MiniCode.Infrastructure` could not host this even if it wanted to: it does not reference `MiniCode.Tools`, so it cannot see `IToolInvoker` at all. Rule of thumb from the project layout holds: the loop is `MiniCode.Agent`'s job.

**An identical call, back to back, cannot produce new information.** Every one of MiniCode's tools is deterministic for the same input unless something else changed the file in between — so refusing an exact repeat costs nothing real. Comparing arguments has to serialize them, not call `ToString()` on them: an array's `ToString()` prints its type name, not its contents, so two different `run_command` calls with different argument arrays would look identical if compared that way.

**A hard ceiling on total calls is the backstop for a model that never repeats exactly but still wanders.** Varying one argument slightly every time defeats a repeat check completely; a flat count per request does not care what the pattern looked like.

**The guard resets once per request, not once per session.** Its state must not survive into whatever the user asks next — a second, unrelated request for the same command a minute later is not a repeat of anything.

**What this Module deliberately does not do.** The guard catches an exact repeat and a runaway count. It does not catch a short cycle — edit file A, edit it back, edit A again — because no individual call ever repeats the one immediately before it. That gap is the Exercise.

## The Code

### `src/MiniCode.Agent/LoopGuardInvoker.cs`

```csharp
using System.Text.Json;
using MiniCode.Tools;

namespace MiniCode.Agent;

/// <summary>
/// Stops the model from spinning: a hard cap on tool calls for one request,
/// and a refusal for a call repeated with identical arguments. Wraps
/// <see cref="IToolInvoker"/> exactly the way Module 11b's
/// AllowListedShellExecutor wraps IShellExecutor — every call passes through
/// here, whether or not this guard has anything to say about it.
/// </summary>
public sealed class LoopGuardInvoker(IToolInvoker inner) : IToolInvoker
{
    /// <summary>The most tool calls one request may make before it is cut off.</summary>
    public const int MaxCallsPerRequest = 20;

    private int _calls;
    private string? _lastCall;

    /// <summary>Clears state. Call once per user request, before the model starts.</summary>
    public void Reset()
    {
        _calls = 0;
        _lastCall = null;
    }

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(ToolInvocation invocation, CancellationToken cancellationToken)
    {
        if (++_calls > MaxCallsPerRequest)
        {
            return $"Stopped after {MaxCallsPerRequest} tool calls in this request. "
                 + "Summarize what you found or changed instead of continuing.";
        }

        string call = invocation.Name + JsonSerializer.Serialize(invocation.Arguments);
        if (call == _lastCall)
        {
            return $"'{invocation.Name}' was just called with these exact arguments. Repeating it will not "
                 + "produce new information — try a different command, argument, or file.";
        }

        _lastCall = call;
        return await inner.InvokeAsync(invocation, cancellationToken);
    }
}
```

The reset, from `src/MiniCode.Agent/CodingAgent.cs` — one line at the top of the method that already ran every request:

```csharp
    public async IAsyncEnumerable<string> RunStreamingAsync(
        string request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _guard.Reset();
        _usedTokens += TokenEstimator.Estimate(request);
```

And the new second half of `Core`, from `src/MiniCode.Agent/AgentInstructions.cs` — the rest of the file, `Floor` and `Compose`, is unchanged:

```csharp
        + "retrying it unchanged. When fixing a problem, run the relevant command — "
        + "usually dotnet build or dotnet test — before guessing at a cause, and run "
        + "it again after editing to confirm the fix actually worked; do not declare "
        + "something fixed without rerunning it. If a command fails the same way "
        + "twice, stop and explain what you tried instead of repeating it.";
```

`CodingAgentFactory` builds one `LoopGuardInvoker` wrapping a `DirectToolInvoker`, passes it to `ToolCatalog` exactly where `DirectToolInvoker` used to go, and passes the same instance to `CodingAgent`'s constructor so `RunStreamingAsync` can reset it.

## Walkthrough

1. **`_guard.Reset()` runs once, before the `await foreach`** — one guard state per request, not per streamed fragment.
2. **MAF's unit is the iteration (a model round trip, which can itself make several tool calls); this Module's unit is the call.** Different things being counted is why 20 is deliberately well under MAF's 40, not equal to it.
3. **`JsonSerializer.Serialize(invocation.Arguments)`, never `.ToString()` on the raw values.** Verified directly: an `AIFunctionArguments` holding a `string[]` prints `System.String[]` from `ToString()` regardless of contents, but serializes to the real JSON array — which is the only thing that actually distinguishes `["build"]` from `["nuget","push"]`.
4. **Both checks run before `inner.InvokeAsync` is called.** A refused call — capped or repeated — never reaches `DirectToolInvoker`, never starts a process, never touches a file.
5. **`LoopGuardInvoker` lives in `MiniCode.Agent`, not `MiniCode.Tools`, though it implements an interface Tools owns.** `MiniCode.Infrastructure` could not have hosted it regardless — it has no reference to Tools and cannot see `IToolInvoker`. Module 15's approval and Module 17's tracing will land in the same project for the same reason: the loop, and everything that governs it, is the Agent's.
6. **`AgentInstructions.Core` changes for the first time since Module 7.** No tool changed to justify it — the model's own idea of what "done" looks like did.

## Exercise

**Detect a short cycle, not just an immediate repeat.** Today, alternating between two different calls — edit file A, edit it back, edit A again — never trips `LoopGuardInvoker`, because neither call ever repeats the one directly before it. Extend the guard to remember the last few call keys (four is enough) and refuse a call that already appears among them, not only one that matches the single most recent call.

Acceptance criteria: the four existing behaviors are unaffected — the shipped tests for the cap, the exact repeat, differing arguments, and `Reset()` all still pass; a period-2 oscillation (`A, B, A`) is refused on that third call; three or more genuinely different calls in a row are never refused. Detecting a longer cycle than two is not required. This belongs in `src/MiniCode.Agent/LoopGuardInvoker.cs`. No later Module needs this — Module 13 calls the same tools without hitting this pattern.

## Expected Output

The guard's two messages are real output, captured by driving `LoopGuardInvoker` directly against the real `run_command` tool — no model, no API key, needed for this part:

```text
=== first call: dotnet build ===
Exit code 0
  Determining projects to restore...
  All projects are up-to-date for restore.
  Shop -> ...\bin\Debug\net10.0\Shop.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:00.90

=== exact repeat: dotnet build again, nothing changed ===
'run_command' was just called with these exact arguments. Repeating it will not produce new information — try a different command, argument, or file.

=== after Reset(): the 21st call in a fresh request ===
Stopped after 20 tool calls in this request. Summarize what you found or changed instead of continuing.
```

The Lab itself — `> Find and fix the build problem.` against a repository with a dropped semicolon — needs a live `OPENAI_API_KEY`, since it is the model, not this lesson's code, driving the sequence. What it produces is not new behavior: `run_command` returns the same `error CS1002: ; expected` text Module 11a's own Expected Output captured, the model locates and edits the line, and `run_command` runs again to confirm — `Exit code 0` where it was `1`. Nothing here is a new tool call; Module 12 is what lets that sequence happen unattended, inside one request, instead of needing a human to drive each step and decide when to stop.
