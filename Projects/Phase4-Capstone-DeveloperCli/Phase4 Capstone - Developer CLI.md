# Phase 4 Capstone — Developer CLI (v0.4)

## Project Overview

Phase 4 gave MiniCode the things that make it a tool rather than a demo: Git, human approval, slash commands, plan and review modes, a stderr trace, and a `dotnet tool install`. This Capstone consolidates them into v0.4 by fixing three faults that only exist *between* those Modules — none of which any single Module could have seen.

## Prerequisites

**Starting point:** open `Module18-PackagingMiniCode/`.

Everything from Phase 4. This is the last Capstone; Module 19 builds the course capstone on top of what ships here.

## Setup

Nothing new. No package, no environment variable, no project. Every change is to code that already exists.

## Core Concepts

**A Phase is where the interactions surface.** A Module is written against its own topic list and verified on its own terms, and it can be entirely correct on those terms while still leaving a gap that only opens once the next Module lands beside it. All three fixes below are that shape — each one is the seam between two Modules that were individually right.

**Seam one: a turn that dies mid-stream leaves no trace.** Module 17 told operators to capture a session with `2> trace.log`, and Module 16a wrapped the loop in a broad `catch` that prints `Error: ...` to stdout. Put those together and the trace — the thing meant to be the record of what happened — is the one place the failure does not appear. It ends at the last thing that worked, with no indication that anything went wrong at all.

**Cancellation is the same hole, and not the same event.** Ctrl+C also ends the stream, and Module 16a already distinguishes it in the terminal: `Cancelled.`, not `Error:`. The trace should keep that distinction rather than filing a deliberate interruption as a fault.

**`yield return` cannot live inside a `try` that has a `catch`.** This is a C# language restriction, not a MAF one — `error CS1626`. An iterator that wants to catch mid-enumeration has to drive the enumerator by hand, so the `try` covers only the move and every `yield` sits outside it. It is more code than `await foreach` and it is the reason this fix did not simply happen in Module 17.

**Seam two: half of what MiniCode does was never traced.** Module 17 instrumented `RunStreamingAsync`, which was the whole story when it was written. Module 16b then added `/plan` and `/review` — genuine model calls that bypass that method entirely. `TracingInvoker` still logs the tools they use, so `/review` shows its `git_diff`; what is missing is any `[Agent]` line saying why the tool ran. Module 16b shipped before Module 17 and Module 17 only knew about the streaming loop, so neither could see it.

**Seam three: two ways of reading an environment variable, one line apart.** Module 18 added `MINICODE_MODEL` with `is { Length: > 0 }` and said why — an exported-but-empty variable means "unset", and nobody should be asking OpenAI for a model named `""`. It left the `OPENAI_API_KEY` guard one line above exactly as Module 3 wrote it: a `??`, which catches only null. So the reasoning Module 18 applied to the new variable was never applied to the old one, and `OPENAI_API_KEY=` skipped the friendly message and reached `OpenAIClient`, which threw an unhandled `ArgumentException` and a stack trace. Packaging is what exposed it — an installed tool gets run from shells that export empty variables.

**The trace format constrains how a failure is logged.** `BracketLoggerProvider` writes `formatter(state, exception)`, and the default formatter renders only the message template — an exception passed as the `LogError` exception argument never reaches the output. So the exception *type* has to be in the template, or the trace says `Task failed` and nothing more.

**What v0.4 is.** A coding agent you install once and run anywhere: it reads and writes inside a workspace boundary, runs allow-listed commands, uses Git, plans before it acts, asks before it changes anything, answers eleven slash commands, and writes a complete stderr trace of what it did. It can also commit on its own initiative — Module 16a added `git commit` to the allow list so `/commit` would work, and that list is shared with the model's own `run_command`, so v0.4 can decide to commit mid-turn as well. Module 15's approval gates it either way, but no lesson has said this out loud, and it is a real consequence of a change made for a different reason.

**What v0.4 is not.** The plan is still not driven. `TaskPlan.WithState`, `Current`, `IsComplete` and `Revise` have been written and unused since Module 9. The Phase 3 Capstone said Module 16 would replace that with a real driver; Module 16 gave the plan a slash command and left the state machine alone. It is still called from nowhere in `src/`, and this Capstone does not change that — re-promising it is the pattern that already failed once, so it is Exercise Two instead. Beyond that: one agent, one session, one workspace, with no sub-agents, no parallel tool calls and no persistence across restarts — `/clear` and process exit are equally final. And the trace is text on stderr, with no structured events, no correlation IDs and no OpenTelemetry.

## The Code

### `src/MiniCode.Cli/MiniCodeVersion.cs`

```csharp
namespace MiniCode.Cli;

/// <summary>
/// The version MiniCode reports at startup. It moves at a Phase Capstone and
/// nowhere else: 0.1 was the read-only Repository Explorer, 0.2 was the Planning
/// Agent, 0.3 was the Autonomous Fixer, 0.4 is the Developer CLI that closes
/// Phase 4. It lives in the CLI because the banner is terminal output. The
/// package version in MiniCode.Cli.csproj is a separate literal that moves with
/// this one by discipline — nothing reads either from the other.
/// </summary>
internal static class MiniCodeVersion
{
    public const string Current = "0.4";
}
```

`<Version>` in `src/MiniCode.Cli/MiniCode.Cli.csproj` moves with it, `0.3.0` to `0.4.0`.

Seam one, the streaming loop in `src/MiniCode.Agent/CodingAgent.cs`:

```csharp
    public async IAsyncEnumerable<string> RunStreamingAsync(
        string request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _guard.Reset();
        _usedTokens += TokenEstimator.Estimate(request);
        _agentLog.LogInformation("Task started");

        // C# forbids `yield return` inside a try that has a catch, so the loop
        // drives the enumerator by hand: the try covers only the move, and every
        // yield sits outside it.
        await using IAsyncEnumerator<AgentResponseUpdate> updates =
            _agent.RunStreamingAsync(request, _session, cancellationToken: cancellationToken)
                  .GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            AgentResponseUpdate update;
            try
            {
                if (!await updates.MoveNextAsync())
                {
                    break;
                }

                update = updates.Current;
            }
            catch (OperationCanceledException)
            {
                // Ctrl+C is a decision, not a fault. Rethrowing is what leaves
                // ConsoleChatLoop's "Cancelled." message intact.
                _agentLog.LogInformation("Task cancelled");
                throw;
            }
            catch (Exception ex)
            {
                // BracketLogger renders only the message template, so the exception
                // type has to be in the template to reach the trace at all.
                _agentLog.LogError("Task failed: {Type}: {Message}", ex.GetType().Name, ex.Message);
                throw;
            }

            string fragment = update.ToString();
            _usedTokens += TokenEstimator.Estimate(fragment) + TokenEstimator.Estimate(ToolActivityText(update));

            if (fragment.Length > 0)
            {
                // TracingInvoker logs every tool call already, so only the model's
                // own words are traced here — logging the tool announcement below
                // too would report the same call under two different categories.
                _agentLog.LogInformation("{Text}", fragment.Trim());
                yield return fragment;
                continue;
            }

            // A tool-only update has no TextContent for ToString() to show, so the
            // operator would otherwise watch nothing happen for as long as the call
            // takes. Show the call itself in its place.
            string toolCalls = DescribeToolCalls(update);
            if (toolCalls.Length > 0)
            {
                yield return toolCalls;
            }
        }

        _agentLog.LogInformation("Task completed");
        if (_budget.ShouldCompact(_usedTokens))
        {
            _usedTokens -= SessionCompactor.Compact(_session);
        }
    }
```

Seam two, also in `src/MiniCode.Agent/CodingAgent.cs` — the two model calls that were never traced:

```csharp
    public async Task<TaskPlan> PlanAsync(string request, CancellationToken cancellationToken = default)
    {
        _agentLog.LogInformation("Planning: {Request}", request);
        TaskPlan plan = await _planner.CreateAsync(request, cancellationToken);
        _agentLog.LogInformation("Plan: {Count} steps", plan.Tasks.Count);
```

```csharp
        _agentLog.LogInformation("Reviewing unstaged changes");
        string diff = await InvokeToolAsync("git_diff", new Dictionary<string, object?>(), cancellationToken);
        string report = await _reviewer.ReviewAsync(diff, cancellationToken);
        _agentLog.LogInformation("Review complete");
```

`ClearSession` gains one line in the same spirit — `_agentLog.LogInformation("Session cleared");` after the history is emptied.

Seam three, in `src/MiniCode.Agent/CodingAgentFactory.cs`:

```csharp
        // Both reads test Length, not null: an exported-but-empty variable is a
        // shell's way of saying "unset", and OPENAI_API_KEY= reaching OpenAIClient
        // raises an unhandled ArgumentException instead of the message below.
        string apiKey = Environment.GetEnvironmentVariable(ApiKeyVariable) is { Length: > 0 } key
            ? key
            : throw new InvalidOperationException(
                $"{ApiKeyVariable} is not set. Set it before running MiniCode.");
```

## Walkthrough

1. **Four files differ from Module 18** — `CodingAgent.cs`, `CodingAgentFactory.cs`, `MiniCodeVersion.cs` and `MiniCode.Cli.csproj`. Everything else in the solution is byte-for-byte what Module 18 shipped.
2. **`GetAsyncEnumerator(cancellationToken)` replaces `await foreach`, and nothing else about the loop changes.** The body below the `try` is the same code Module 17 left behind, indented one level further. `await using` on the enumerator is what `await foreach` was doing invisibly.
3. **The `catch` blocks rethrow, so the terminal behaves exactly as before.** `ConsoleChatLoop` still prints `Cancelled.` or `Error: ...`. This fix adds a line to the trace; it changes nothing the user sees.
4. **`break` inside the `try` is fine — it is `yield` that the compiler refuses.** Only the `yield return` statements had to move out.
5. **`Task completed` now means what it says.** A run that faults or is cancelled never reaches it, so the trace no longer implies a turn finished when it did not.
6. **`PlanAsync` is called on every ordinary chat turn too**, not only by `/plan` — so a normal turn's trace now opens with `Planning:` and `Plan: N steps` before `Task started`. That call was always happening and always costing tokens; it simply was not visible.
7. **The `/review` trace reads end to end for free.** `Reviewing unstaged changes`, then `TracingInvoker`'s existing `[Tool] git_diff` and `[Result] ...`, then `Review complete`.
8. **Every new log call is a single line.** `BracketLogger` prefixes the category once per call, so logging a rendered plan would produce one bracketed line followed by bare ones. `Plan: {Count} steps` is a summary for that reason.
9. **Seam one covers the loop, not every model call.** `PlanAsync` and `ReviewAsync` can fault too, in a single call with no iterator to guard. This Capstone leaves those endings untraced.

## Exercise

**One:** trace the calls seam one does not cover. `PlanAsync` and `ReviewAsync` are single awaits, so an ordinary `try`/`catch` works where the iterator needed a hand-driven enumerator. Give each the same treatment in `src/MiniCode.Agent/CodingAgent.cs`. Acceptance criteria: a fault in either logs one `[Agent]` line naming the exception type and message, then rethrows so `ConsoleChatLoop` prints what it prints today; a cancellation logs as a cancellation, not a failure; a successful call's trace is unchanged.

**Two:** drive the plan. Make `RunStreamingAsync` advance the plan it was given — mark a step `Complete` as the model finishes it, and use `TaskPlan.Revise` when the model's actual course diverges from the plan. Acceptance criteria: `/status` after a turn shows which steps were completed; a plan whose steps all complete reports `IsComplete`; revising never renumbers or removes a step already shown to the operator; `TaskPlan`'s immutability is preserved, so each change produces a new instance. This is a stretch beyond what MiniCode requires, and the acceptance criteria are the specification.

Neither has a reference implementation in a later Module folder.

## Expected Output

The trace captures below are real output, not typed by hand, and it is worth being exact about what produced them. `CodingAgent`'s constructor is internal and building one needs a live chat client, so the seam-one traces come from the loop above reproduced line for line against a real `ChatClientAgent` whose chat client was scripted to emit one update and then fail — the way a dropped connection behaves. The seam-two trace is the log calls those three methods now make, sent through this Capstone's own `BracketLoggerProvider` and its own `TracingInvoker` around a stubbed `git_diff`. Both use the shipped logging code and the shipped trace format; no request was sent to OpenAI for any capture on this page.

That is also why the seam-one traces below start at `Task started` rather than the `Planning:` lines Walkthrough 6 describes — the planner is a live model call, and nothing on this page makes one.

A turn that dies mid-stream. What the operator sees on stdout, exactly as before:

```text
Reading Program.cs
Error: The response ended prematurely.
```

And what `2> trace.log` now captures — the line that did not exist before v0.4:

```text
[Agent] Task started
[Agent] Reading Program.cs
[Agent] Task failed: HttpRequestException: The response ended prematurely.
```

Ctrl+C during the same turn, filed as a decision rather than a fault, and `Task completed` correctly absent from both:

```text
[Agent] Task started
[Agent] Reading Program.cs
[Agent] Task cancelled
```

Seam two — `/review`, `/plan` and `/clear`, which produced no `[Agent]` line at all in Module 18:

```text
[Agent] Reviewing unstaged changes
[Tool] git_diff
[Result] diff --git a/src/MiniCode.Cli/Program.cs b/src/MiniCode.Cli/Program.cs (23ms)
[Agent] Review complete
[Agent] Planning: add caching to the price lookup
[Agent] Plan: 3 steps
[Agent] Session cleared
```

Seam three, from the actually-installed tool, with `OPENAI_API_KEY` first removed from the environment and then set to an empty string. Both now reach the same message; in Module 18 the second printed an unhandled `ArgumentException` and a stack trace:

```text
> minicode
OPENAI_API_KEY is not set. Set it before running MiniCode.
```

And v0.4 packed, installed to an isolated tool path and run from a directory outside this repository — nothing from this capture is still installed:

```text
> dotnet pack src/MiniCode.Cli -c Release -o ./nupkg
  Successfully created package '...\nupkg\MiniCode.0.4.0.nupkg'.

> dotnet tool install --tool-path ./toolpath --add-source ./nupkg MiniCode --version 0.4.0
You can invoke the tool using the following command: minicode
Tool 'minicode' (version '0.4.0') was successfully installed.
```

```text
> cd CustomerPortal
> minicode
MiniCode v0.4
Workspace: C:\Users\...\CustomerPortal
MiniCode. Type 'exit' to quit, or /help for commands.

>
```

That banner is where Phase 4 ends: a version number that moved, printed by a command on the path, in a repository that knows nothing about this course.
