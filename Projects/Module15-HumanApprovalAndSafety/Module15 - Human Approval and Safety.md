# Module 15 — Human Approval and Safety

## Project Overview

Since Phase 3, MiniCode has edited files and run commands with nobody watching — video-plan.md called that gap out explicitly, and this Module is its answer. `ApprovalInvoker` pauses in front of the tool calls that change something, asks a human, and only proceeds on a yes.

## Prerequisites

**Starting point:** open `Module14-GitIntegration/`.

Module 5a's `IToolInvoker` seam — its own doc comment has named this Module as the reason it exists since it was written. Module 12's `LoopGuardInvoker`, which this now sits inside.

Second Module of Phase 4. Ships as part of **Phase 4 Capstone — Developer CLI (v0.4)**.

## Setup

Nothing to install. No new package.

## Core Concepts

**The syllabus's safe/unsafe split, mapped onto MiniCode's actual tools.** Safe: every read-only tool — `read_file`, `list_files`, `search_files`, `describe_repository`, `workspace_root`, `git_status`, `git_diff`, `git_log` — plus `run_command` when it is exactly `dotnet build` or `dotnet test`. Everything else requires a yes: `write_file`, `edit_file`, and any other shape of `run_command` — which is where `git commit`, `git push`, a package install, or an unrecognized shell command all actually arrive, since MiniCode has no dedicated tool for any of them.

**Approval is one more decorator around the same seam.** `ApprovalInvoker` implements `IToolInvoker` exactly as `LoopGuardInvoker` and `AllowListedShellExecutor` already do — wrap, don't touch. No tool's code changes.

**Where it sits in the chain matters.** `LoopGuardInvoker` wraps `ApprovalInvoker`, which wraps `DirectToolInvoker`. A call the loop guard would refuse for repeating never reaches a human — nobody is asked the same question twice for a call MiniCode was about to be told not to repeat anyway. A call still waiting on an answer still counts toward the loop's 20-call cap.

**The interface lives where it's needed; the terminal lives where terminals live.** `IApprovalPrompter` is declared in `MiniCode.Agent`, next to the invoker that calls it. `ConsoleApprovalPrompter` — the only class in the solution that reads a real answer from a person — lives in `MiniCode.Cli`, and `CodingAgentFactory` receives it as a parameter instead of constructing it, the first time this course has had the composition root take a collaborator from its caller rather than building every piece itself.

**A refusal is a string, not an exception.** Denying a call returns `"{name} was not approved and did not run."` — the model reads it and reacts exactly the way it already reacts to Module 11b's allow-list refusal, because both paths return the same idea: not this, try something else.

**"Always" is one flag for the session, not a memory of what was asked.** The syllabus's example offers `[A] Always allow for this session` — the simplest thing that satisfies it is a single `bool` that, once set, skips every future check for as long as this `ApprovalInvoker` instance lives, which is exactly one session.

## The Code

### `src/MiniCode.Agent/ApprovalDecision.cs`

```csharp
namespace MiniCode.Agent;

/// <summary>What the operator decided about one pending tool call.</summary>
public enum ApprovalDecision
{
    /// <summary>Run this call, and ask again next time.</summary>
    Approve,

    /// <summary>Refuse this call. The model is told, in words, that it was not approved.</summary>
    Deny,

    /// <summary>Run this call, and stop asking for the rest of the session.</summary>
    AlwaysApprove,
}
```

### `src/MiniCode.Agent/IApprovalPrompter.cs`

```csharp
namespace MiniCode.Agent;

/// <summary>
/// Asks a human before a tool call nobody has already approved for this
/// session. <c>MiniCode.Cli</c> is the only place this is ever answered from a
/// real console — everything else in the solution sees only this interface.
/// </summary>
public interface IApprovalPrompter
{
    /// <summary>Shows the pending call and returns what the operator decided.</summary>
    Task<ApprovalDecision> AskAsync(string description, CancellationToken cancellationToken);
}
```

### `src/MiniCode.Agent/ApprovalInvoker.cs`

```csharp
using System.Text.Json;
using MiniCode.Tools;

namespace MiniCode.Agent;

/// <summary>
/// Wraps Module 5a's <see cref="IToolInvoker"/> seam and pauses in front of the
/// calls the syllabus marks as needing a human: writing to the repository, and
/// any shell command other than a build or test run. Once the operator answers
/// "always", nothing is asked again for the rest of the session.
/// </summary>
public sealed class ApprovalInvoker(IToolInvoker inner, IApprovalPrompter prompter) : IToolInvoker
{
    private bool _alwaysApprove;

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(ToolInvocation invocation, CancellationToken cancellationToken)
    {
        if (_alwaysApprove || !RequiresApproval(invocation))
        {
            return await inner.InvokeAsync(invocation, cancellationToken);
        }

        ApprovalDecision decision = await prompter.AskAsync(Describe(invocation), cancellationToken);
        _alwaysApprove = decision == ApprovalDecision.AlwaysApprove;

        return decision == ApprovalDecision.Deny
            ? $"{invocation.Name} was not approved and did not run."
            : await inner.InvokeAsync(invocation, cancellationToken);
    }

    private static bool RequiresApproval(ToolInvocation invocation) => invocation.Name switch
    {
        "write_file" or "edit_file" => true,
        "run_command" => !IsBuildOrTest(invocation),
        _ => false,
    };

    // A pending call's arguments can already be a JsonElement rather than a CLR
    // string[] — the same shape the Phase 3 Capstone's WorkingState note had to
    // account for. Serializing through JsonElement normalizes both.
    private static bool IsBuildOrTest(ToolInvocation invocation)
    {
        JsonElement arguments = JsonSerializer.SerializeToElement(invocation.Arguments);
        return arguments.TryGetProperty("command", out JsonElement command)
            && string.Equals(command.GetString(), "dotnet", StringComparison.OrdinalIgnoreCase)
            && arguments.TryGetProperty("arguments", out JsonElement args)
            && args.ValueKind == JsonValueKind.Array
            && args.GetArrayLength() > 0
            && args[0].GetString() is "build" or "test";
    }

    private static string Describe(ToolInvocation invocation) =>
        $"{invocation.Name}({JsonSerializer.Serialize(invocation.Arguments)})";
}
```

### `src/MiniCode.Cli/ConsoleApprovalPrompter.cs`

```csharp
using MiniCode.Agent;

namespace MiniCode.Cli;

/// <summary>
/// Answers an approval prompt on the real console. This is the only place in
/// the solution that reads a Y/N/A answer from a person.
/// </summary>
internal sealed class ConsoleApprovalPrompter : IApprovalPrompter
{
    /// <inheritdoc />
    public Task<ApprovalDecision> AskAsync(string description, CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine("MiniCode wants to run:");
        Console.WriteLine(description);
        Console.Write("Allow? [Y] Yes  [N] No  [A] Always allow for this session: ");

        ApprovalDecision decision = Console.ReadLine()?.Trim().ToUpperInvariant() switch
        {
            "A" => ApprovalDecision.AlwaysApprove,
            "Y" => ApprovalDecision.Approve,
            _ => ApprovalDecision.Deny,
        };

        return Task.FromResult(decision);
    }
}
```

`CodingAgentFactory.CreateAsync` gains an `IApprovalPrompter prompter` parameter. A new line builds `new ApprovalInvoker(new DirectToolInvoker(), prompter)`, and the line after it — previously `new LoopGuardInvoker(new DirectToolInvoker())` — now wraps that invoker instead: `new LoopGuardInvoker(approval)`. `Program.cs` passes `new ConsoleApprovalPrompter()` at the one call site that builds the agent.

## Walkthrough

1. **`RequiresApproval` is a two-line switch.** Everything not named `write_file`, `edit_file`, or an unrecognized `run_command` falls through to `false` — new safe tools (Module 14's three Git tools included) need no entry here at all.
2. **`IsBuildOrTest` reads through `JsonElement`, never casts to `string[]`.** `ApprovalInvoker` sees a call before `AIFunctionFactory` has bound it to `ShellTools.RunCommand`'s typed parameters, so an argument here can already be a `JsonElement` rather than the CLR array a unit test might construct — serializing first and reading back as `JsonElement` handles both.
3. **`Describe` reuses the exact serialization Module 12's loop guard already established** for comparing calls — `JsonSerializer.Serialize(invocation.Arguments)`, not `.ToString()` — so what a human is shown is never the array's type name.
4. **Denial short-circuits before `inner.InvokeAsync` is ever awaited.** The tool genuinely does not run; the string returned is the only trace of the attempt.
5. **`_alwaysApprove` is instance state, not a static.** `CodingAgentFactory` builds one `ApprovalInvoker` per call to `CreateAsync`, and `CreateAsync` runs once per `MiniCode` process — so "for this session" and "for the lifetime of this object" are the same thing without any extra bookkeeping.
6. **`ConsoleApprovalPrompter` is `internal`,** like `ConsoleChatLoop` before it — nothing outside `MiniCode.Cli` is meant to construct one directly.

## Exercise

`_alwaysApprove` is one flag for every kind of call — answering "always" to a `write_file` prompt also silences the very next `git push`. Change `ApprovalInvoker` to remember "always" per tool name instead of globally, in `src/MiniCode.Agent/ApprovalInvoker.cs`. Acceptance criteria: answering "always" to a `write_file` prompt means later `write_file` calls are never asked again, but a later `run_command` that needs approval still is; a fresh `ApprovalInvoker` starts with nothing remembered; denying a call does not count as "always" for anything. No reference implementation ships in a later Module folder — the course itself never exercises this distinction.

## Expected Output

All of the following is real output from this Module's own `ApprovalInvoker`, driven directly against a scripted `IApprovalPrompter` — no model call involved, since the routing logic is deterministic.

A safe read tool never prompts:

```text
ran: read_file
Prompted: 0 time(s)
```

Neither does a recognized build or test run, even though it is `run_command`:

```text
ran: run_command
Prompted: 0 time(s)
```

`write_file`, denied — the tool never runs:

```text
MiniCode wants to run:
write_file({"path":"src/CustomerService.cs","content":"..."})
Allow? [Y] Yes  [N] No  [A] Always allow for this session: (scripted: Deny)
write_file was not approved and did not run.
```

`git push` — reaching `run_command` in a shape that is not a build or test — answered "always", after which a later `write_file` needs no second prompt:

```text
MiniCode wants to run:
run_command({"command":"git","arguments":["push"]})
Allow? [Y] Yes  [N] No  [A] Always allow for this session: (scripted: AlwaysApprove)
ran: run_command
ran: write_file
Prompted: 1 time(s)
```

The Lab's own scenario — MiniCode asking permission before `dotnet add package Microsoft.EntityFrameworkCore.SqlServer` — needs a live `OPENAI_API_KEY` and a real terminal, since it is `ConsoleApprovalPrompter` reading an actual keystroke, not this lesson's code. What reaches it is exactly the fourth capture above with a different `run_command`: the model calls it, `ApprovalInvoker` sees a shell command that is not `dotnet build` or `dotnet test`, and the operator sees the same three-choice prompt before anything is installed.
