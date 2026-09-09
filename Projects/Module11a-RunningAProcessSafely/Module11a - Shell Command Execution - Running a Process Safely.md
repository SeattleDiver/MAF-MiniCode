# Module 11a — Shell Command Execution: Running a Process Safely

## Project Overview

MiniCode can read a repository and edit it, but it has never been able to check whether an edit actually works. This Module gives it `run_command`: a tool that starts an external program directly — no shell, an enforced timeout, captured output — so the agent can build what it changed. Module 11b decides which commands it is allowed to ask for; this one is only about running one without losing control of it.

## Prerequisites

**Starting point:** open `Module10-SafeFileEditing/`.

Module 5a's interception seam — `run_command` is wrapped by the same `InterceptedFunction` as every read and write tool, with no special case for it. Module 4's `IWorkspace.Root` is where every command runs; there is no working-directory argument yet for the model to redirect.

First half of Module 11. Ships as part of **Phase 3 Capstone — Autonomous Fixer (v0.3)**, once Module 11b adds the allow list.

## Setup

Nothing to install. `System.Diagnostics.Process` is in the shared framework, so `MiniCode.Infrastructure` gets its first files here and still has zero package references.

## Core Concepts

**A tool that can only read has a small blast radius. A tool that can run any program does not.** `dotnet build` and a destructive command are the same C# call away. "Safely" in this Module's title is about the mechanics — no shell, a timeout, output you can actually read — not yet about which commands are allowed. That restriction is Module 11b.

**No shell means no shell injection.** `ProcessStartInfo.ArgumentList` sends arguments to the operating system as a list, one slot each. There is no command-line string for a stray quote or `&&` to break out of, because a command-line string is never built in the first place.

**A process that never finishes is a resource leak wearing a mask of patience.** Every run gets a timeout. Running past it does not mean waiting forever — it means the process is killed and the tool says so in words the model can read and react to.

**Timing out and being cancelled are different endings for the same race.** A timeout is expected: the model asked for something too slow and gets a result back. Cancellation is the whole run being abandoned, and Module 5a's rule for that has not changed — it propagates, it does not turn into tool output.

**Read both streams while the process is still running, not after it exits.** A command that writes enough to stderr will block on a full pipe if nothing is draining it. Reading stdout and stderr concurrently, as two tasks running alongside the process, is what keeps a chatty command from deadlocking against its own output.

**Every command runs in the workspace root, and the model cannot ask for another one.** There is no path argument here to validate — the safety property is that there is no path input at all yet.

**What this Module deliberately does not do.** `run_command` will run anything the model names — `git push`, a package install, a destructive command, whatever it asks for. Module 11b puts an allow list in front of it before Module 12 lets a loop call this unattended.

## The Code

### `src/MiniCode.Infrastructure/IShellExecutor.cs`

```csharp
namespace MiniCode.Infrastructure;

/// <summary>
/// Runs an external process to completion, or kills it. The only thing in
/// MiniCode that calls <see cref="System.Diagnostics.Process"/> directly.
/// </summary>
public interface IShellExecutor
{
    /// <summary>
    /// Runs <paramref name="command"/> with <paramref name="arguments"/> inside
    /// <paramref name="workingDirectory"/>, killing it if it outruns
    /// <paramref name="timeout"/>.
    /// </summary>
    Task<ShellCommandResult> RunAsync(
        string command,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
```

### `src/MiniCode.Infrastructure/ShellCommandResult.cs`

```csharp
namespace MiniCode.Infrastructure;

/// <summary>
/// What running a command produced. <see cref="ExitCode"/> is <c>null</c>
/// exactly when <see cref="TimedOut"/> is <c>true</c> — a killed process never
/// exits, so it never has one.
/// </summary>
public sealed record ShellCommandResult(int? ExitCode, string StandardOutput, string StandardError, bool TimedOut);
```

Starting the process, from `src/MiniCode.Infrastructure/ShellExecutor.cs` — no shell, arguments as a list:

```csharp
        var startInfo = new ProcessStartInfo(command)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
```

And the timeout race, from the same file — this is the whole safety mechanism:

```csharp
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            cancellationToken.ThrowIfCancellationRequested();
            return new ShellCommandResult(null, await stdout, await stderr, TimedOut: true);
        }

        return new ShellCommandResult(process.ExitCode, await stdout, await stderr, TimedOut: false);
```

The model-facing side, from `src/MiniCode.Tools/ShellTools.cs`:

```csharp
    public async Task<string> RunCommand(
        [Description("The program to run, e.g. \"dotnet\". Not passed through a shell.")] string command,
        [Description("Arguments to pass, e.g. [\"build\"]. Omit for none.")] string[]? arguments = null,
        [Description("Seconds to allow before the process is killed.")] int timeoutSeconds = 120,
        CancellationToken cancellationToken = default)
    {
        ShellCommandResult result = await shell.RunAsync(
            command, arguments ?? [], workspace.Root, TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);

        if (result.TimedOut)
        {
            return $"'{command}' did not finish within {timeoutSeconds}s and was killed.";
        }

        var text = new StringBuilder($"Exit code {result.ExitCode}");
        Append(text, result.StandardOutput);
        Append(text, result.StandardError, "stderr:");
        return text.ToString();
    }
```

A private `Append` helper caps stdout and stderr at 4,000 characters each with a truncation notice — the same idea as the entry cap in `list_files` and `search_files`, applied to process output. `ToolCatalog` gains a fifth constructor parameter, `IShellExecutor shell`, builds a `ShellTools`, and registers `run_command` through the same private `Intercept` as every other tool. `CodingAgentFactory` constructs one `ShellExecutor` and passes it through.

## Walkthrough

1. **`IShellExecutor` is the only thing that touches `Process`.** Everything above it — `ShellTools`, the agent, the model — deals in `ShellCommandResult`, a plain record with no behavior.
2. **`ArgumentList`, never a concatenated string.** Building `"dotnet build " + userInput` would hand a shell metacharacter a place to hide. A list has no such place.
3. **Two `ReadToEndAsync` tasks are started before the process is awaited.** Starting them after `WaitForExitAsync` completes risks a full pipe blocking the exit itself — read concurrently with running, not after.
4. **One `CancellationTokenSource`, built from the caller's token, with `CancelAfter(timeout)` layered on.** If it fires because of the timeout, the external `cancellationToken` is still live, so `ThrowIfCancellationRequested()` does nothing and the method returns a timed-out result. If the caller's own token fired, that line throws and the run propagates — same value, same field, two different reasons to be cancelled.
5. **The kill happens before either outcome is decided.** A timed-out process does not get to keep running in the background just because MiniCode stopped waiting on it.
6. **`AIFunctionFactory` binds `CancellationToken` parameters from the invocation itself and leaves them out of the JSON schema.** The model never sees `cancellationToken` as something it could pass — it is not part of the tool's declared shape.
7. **Exit code, stdout, then stderr, in that order, with a timeout message replacing all three when it applies.** The model reads top to bottom; the fact that changes its next move — did it succeed — comes first.

## Exercise

Add an optional workspace-relative `workingDirectory` parameter to `RunCommand`, in `src/MiniCode.Tools/ShellTools.cs`. Resolve it through `IWorkspace.ResolvePath` — the same call `read_file` and `write_file` already make — before passing it to `shell.RunAsync` in place of `workspace.Root`. Acceptance criteria: omitting the parameter runs at the workspace root exactly as today; passing a subdirectory that exists runs the command there; passing a path outside the workspace is refused with the same `OutsideWorkspace` message a read or write would give, because it goes through the identical boundary. No reference implementation ships in a later Module folder — Module 12 always calls `run_command` at the root, so this parameter is never exercised by the course itself.

## Expected Output

All of the following is real output from this Module's code, calling `ShellTools.RunCommand` against a temporary console project.

A clean build:

```text
Exit code 0
  Determining projects to restore...
  All projects are up-to-date for restore.
  Shop -> ...\bin\Debug\net10.0\Shop.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:01.17
```

The Lab's scenario — a dropped semicolon in `Program.cs`, then the same command again (path prefix trimmed for space; the line, column and message are exactly what `dotnet build` printed):

```text
Exit code 1
  Determining projects to restore...
  All projects are up-to-date for restore.
Program.cs(1,35): error CS1002: ; expected [...\Shop.csproj]

Build FAILED.

Program.cs(1,35): error CS1002: ; expected [...\Shop.csproj]
    0 Warning(s)
    1 Error(s)
```

A command that runs past its timeout — `run_command("ping", ["-n", "6", "127.0.0.1"], timeoutSeconds: 1)`:

```text
'ping' did not finish within 1s and was killed.
```

And a program that does not exist, through the full invoker chain exactly as Module 5a wired it — a throw becomes text, never an unhandled exception:

```text
run_command failed: An error occurred trying to start process 'this-program-does-not-exist' with working directory '...'. The system cannot find the file specified.
```

The exit code and both streams tell the model what happened; the timeout and the missing-program cases each say so in one plain sentence instead. Module 11b decides which of these commands should have been allowed to start at all.
