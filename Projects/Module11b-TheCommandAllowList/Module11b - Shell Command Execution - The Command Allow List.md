# Module 11b — Shell Command Execution: The Command Allow List

## Project Overview

Module 11a made `run_command` run a process correctly — no shell, a timeout, captured output. It never asked whether the process *should* run. This Module puts a fixed allow list in front of it, so `dotnet build` starts and everything else is refused before a process ever exists.

## Prerequisites

**Starting point:** open `Module11a-RunningAProcessSafely/`.

Module 5a's `InterceptedFunction` — this Module reuses its exact shape one layer lower. Module 11a's `IShellExecutor` and `ShellTools` are untouched; nothing about them needed to change.

Second half of Module 11. Ships as part of **Phase 3 Capstone — Autonomous Fixer (v0.3)**.

## Setup

Nothing new. `MiniCode.Infrastructure` still has zero package references.

## Core Concepts

**11a was mechanics; this is policy, and they are now two different classes.** Running a process safely and deciding which processes may run are separate questions with separate answers, and the code finally reflects that: `ShellExecutor` still knows nothing about permission.

**Wrap, don't touch — the same shape as Module 5a, one layer down.** `InterceptedFunction` decorates an `AIFunction` without the inner function ever knowing it is wrapped. `AllowListedShellExecutor` does exactly that to `IShellExecutor`: it holds the real executor, checks first, and only calls through when the call is allowed. `ShellTools` still just takes an `IShellExecutor` — it cannot tell which one it was handed.

**Match the program and its first argument together, not the program name alone.** Allowing bare `"dotnet"` would allow `dotnet nuget push` and `dotnet tool install` under the same entry that was meant to permit `dotnet build`. Pairing the program with its subcommand is what makes an entry mean one specific thing.

**A refusal here is the same shape as every refusal in the course.** Throw a sentence naming what was asked and what is allowed; Module 5a's invoker turns it into text; the model reads it and tries something on the list instead.

**Throwing inside an `async` method is still safe here, same as everywhere else in MiniCode.** `RunAsync` throws before its first `await`, but because the method is declared `async`, the exception rides on the `Task` it returns rather than escaping synchronously — `await shell.RunAsync(...)` sees it exactly where it expects to.

**What this Module deliberately does not do.** The list stops a careless or prompt-injected command from starting. It does not stop a model that decides to get there another way — Module 10 already gave it `write_file`, and a `.csproj` with a custom build target can run anything `dotnet build` is asked to run, on the list by name. Closing that is Module 15's job, not a gap in this one.

## The Code

### `src/MiniCode.Infrastructure/ICommandAllowList.cs`

```csharp
namespace MiniCode.Infrastructure;

/// <summary>Decides which commands <c>run_command</c> is permitted to start.</summary>
public interface ICommandAllowList
{
    /// <summary>The allowed "program subcommand" pairs, for a message the model can read.</summary>
    IReadOnlyList<string> Entries { get; }

    /// <summary>True when <paramref name="command"/> plus its first argument is on the list.</summary>
    bool IsAllowed(string command, IReadOnlyList<string> arguments);
}
```

### `src/MiniCode.Infrastructure/CommandAllowList.cs`

```csharp
namespace MiniCode.Infrastructure;

/// <summary>
/// The fixed set run_command may start. Matched on the program and its first
/// argument together, so allowing "dotnet build" does not also allow
/// "dotnet nuget push" — every other dotnet subcommand stays refused.
/// </summary>
public sealed class CommandAllowList : ICommandAllowList
{
    // One entry per allowed subcommand — "dotnet" alone is deliberately absent,
    // so nothing else the model could put after it is allowed by accident.
    private static readonly string[] Allowed = ["dotnet restore", "dotnet build", "dotnet test", "dotnet format"];

    /// <inheritdoc />
    public IReadOnlyList<string> Entries => Allowed;

    /// <inheritdoc />
    public bool IsAllowed(string command, IReadOnlyList<string> arguments) =>
        arguments.Count > 0
        && Allowed.Contains($"{command} {arguments[0]}", StringComparer.OrdinalIgnoreCase);
}
```

### `src/MiniCode.Infrastructure/AllowListedShellExecutor.cs`

```csharp
namespace MiniCode.Infrastructure;

/// <summary>
/// Refuses a command before <see cref="IShellExecutor"/> ever sees it. The
/// same wrap-don't-touch shape as Module 5a's <c>InterceptedFunction</c>, one
/// layer lower: this decorates <see cref="IShellExecutor"/> instead of an
/// <c>AIFunction</c>, and the inner executor stays unaware it is guarded.
/// </summary>
public sealed class AllowListedShellExecutor(IShellExecutor inner, ICommandAllowList allowList) : IShellExecutor
{
    /// <inheritdoc />
    public async Task<ShellCommandResult> RunAsync(
        string command,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!allowList.IsAllowed(command, arguments))
        {
            string requested = $"{command} {string.Join(' ', arguments)}".TrimEnd();
            throw new InvalidOperationException(
                $"'{requested}' is not on the allow list. Allowed: {string.Join(", ", allowList.Entries)}.");
        }

        return await inner.RunAsync(command, arguments, workingDirectory, timeout, cancellationToken);
    }
}
```

`CodingAgentFactory` now builds `new AllowListedShellExecutor(new ShellExecutor(), new CommandAllowList())` in place of the bare `ShellExecutor` and hands that to `ToolCatalog` exactly as before — `ToolCatalog`'s constructor does not change, because it never depended on which `IShellExecutor` it received. `run_command`'s description in `ToolCatalog` gains one clause: *"Restricted to a fixed allow list; a refusal names what is permitted."*

## Walkthrough

1. **`ICommandAllowList` is the whole seam.** Anything that can answer `IsAllowed` can sit in this slot — a hardcoded list today, something configurable later, without `AllowListedShellExecutor` or `ShellTools` changing.
2. **The check runs before `inner.RunAsync` is ever called.** A refused command never starts a process, never spends the timeout, never touches the filesystem `ShellExecutor` would have run it against.
3. **`Entries` exists so the refusal message can name what *is* allowed**, not just what was refused. A tool that only says no teaches the model nothing about what to try next.
4. **`dotnet --version` is refused too, and that is not a regression.** It was fine in Module 11a because nothing decided otherwise yet. An allow list's entire job is saying what *is* permitted — everything else follows from that, including commands that were harmless.
5. **`OrdinalIgnoreCase` on the match.** The model composes these strings; `"Build"` should not silently fail where `"build"` would have succeeded.
6. **`CommandAllowList` has no constructor parameters and no state beyond the fixed array.** Nothing here reads a file, an environment variable, or the repository's `AGENTS.md` — the list a viewer sees in this file is the complete, only list MiniCode enforces.

## Exercise

**Generalize the match from "program plus first argument" to "entry as a prefix."** Today `IsAllowed` can only express two-token rules. Change it so each entry in `Allowed` is split into its own tokens, and an entry matches when its tokens are a prefix of `[command, ...arguments]` — so an entry `"dotnet tool restore"` permits `dotnet tool restore` without also permitting `dotnet tool install`, which today's two-token check cannot distinguish.

Acceptance criteria: the four shipped entries behave exactly as they do today; adding `"dotnet tool restore"` to `Allowed` allows `run_command("dotnet", ["tool", "restore"])` and still refuses `run_command("dotnet", ["tool", "install", "x"])`; a one-token entry such as `"dotnet"` would match every dotnet invocation, so the acceptance criteria are that nobody adds one, not that the code forbids it. This belongs in `src/MiniCode.Infrastructure/CommandAllowList.cs`. No later Module needs this generalization — Module 12 calls `run_command` with the four shipped entries only.

## Expected Output

All of the following is real output from this Module's code, calling `ShellTools.RunCommand` — through `AllowListedShellExecutor` — against the same temporary console project Module 11a used.

An allowed command runs exactly as it did last lesson:

```text
Exit code 0
  Determining projects to restore...
  All projects are up-to-date for restore.
  Shop -> ...\bin\Debug\net10.0\Shop.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:00.87
```

A subcommand of an allowed program, refused because only its sibling is on the list — through the full invoker chain, so the message is exactly what the model would read:

```text
run_command failed: 'dotnet nuget push' is not on the allow list. Allowed: dotnet restore, dotnet build, dotnet test, dotnet format.
```

And the command that timed out in Module 11a's lesson never gets that far this time — it is refused before a process starts:

```text
run_command failed: 'ping -n 6 127.0.0.1' is not on the allow list. Allowed: dotnet restore, dotnet build, dotnet test, dotnet format.
```

Same executor underneath, same never-throw invoker from Module 5a — the only thing that changed is that something now gets to say no before `Process.Start` is ever called.
