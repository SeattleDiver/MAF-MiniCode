# Module 14 — Git Integration

## Project Overview

MiniCode can now edit files and run commands, but it has no way to see its own trail — everything it knows about "what changed" comes from its own memory of the conversation, which Module 8's compaction can throw away. This Module gives it three read-only tools — `git_status`, `git_diff`, `git_log` — so it can ask Git directly instead of trusting its own recollection. It cannot commit, push, or stage anything yet; that boundary is deliberate.

## Prerequisites

**Starting point:** open `Phase3-Capstone-AutonomousFixer/`.

Module 11a's `IShellExecutor` — Git integration reuses it rather than adding a dependency. Module 5a's interception seam, which wraps these three tools exactly as it wraps every other one.

First Module of Phase 4. Ships as part of **Phase 4 Capstone — Developer CLI (v0.4)**.

## Setup

Nothing to install. No new package — Git integration means running the `git` binary, which the Module assumes is already on `PATH`, the same assumption the course already makes about `dotnet`.

## Core Concepts

**Talking to Git is just running a process.** `MiniCode.Infrastructure` owns shell and Git for the same reason: there is no Git library here, no new package reference, just `git status`, `git diff` and `git log` run through the exact `IShellExecutor` Module 11a already built. `GitClient` is three fixed invocations of it.

**Fixed invocations need no allow list.** Module 11b's allow list exists because `run_command` lets the model choose the program and its arguments. `GitClient` never does — every call it can possibly make is one of three hardcoded argument lists baked into the class itself. There is nothing here for an allow list to guard, so this Module adds none.

**Read-only by construction, not by policy.** Module 15's later split between safe and approval-gated operations lists `git status`/`git diff` as safe and `git commit`/`git push` as gated. This Module does not implement that gate — it simply never writes the code that would call `git commit` in the first place. A capability that does not exist needs no permission check.

**A tool result is not always an exception.** Running `git status` in a directory that is not a Git repository is not a bug — it is Git answering the question truthfully. `GitTools` checks the exit code itself and returns Git's own message as the result, the same never-throw discipline Module 5a established, applied here before an exception would even occur.

**The model is told to check, not to remember.** A "change report" written from the model's memory of what it edited can drift from what is actually on disk — a rejected edit, an untracked file it forgot about. The agent's instructions now say to call `git_status` and `git_diff` before writing one.

## The Code

### `src/MiniCode.Infrastructure/IGitClient.cs`

```csharp
namespace MiniCode.Infrastructure;

/// <summary>
/// The three read-only Git queries MiniCode can make. There is deliberately no
/// method here that changes anything — Module 15 is what adds permission, and
/// there is nothing yet that would need to ask for it.
/// </summary>
public interface IGitClient
{
    /// <summary>Modified, added and untracked files against the current branch.</summary>
    Task<ShellCommandResult> StatusAsync(string workspaceRoot, CancellationToken cancellationToken);

    /// <summary>Unstaged changes to tracked files, in unified diff format.</summary>
    Task<ShellCommandResult> DiffAsync(string workspaceRoot, CancellationToken cancellationToken);

    /// <summary>The most recent commits, newest first.</summary>
    Task<ShellCommandResult> LogAsync(string workspaceRoot, int maxEntries, CancellationToken cancellationToken);
}
```

### `src/MiniCode.Infrastructure/GitClient.cs`

```csharp
namespace MiniCode.Infrastructure;

/// <summary>
/// Git integration is just Module 11a's process runner pointed at a fixed
/// binary: every method here is one hardcoded <c>git</c> invocation, so there
/// is nothing here for a command allow list to guard.
/// </summary>
public sealed class GitClient(IShellExecutor shell) : IGitClient
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public Task<ShellCommandResult> StatusAsync(string workspaceRoot, CancellationToken cancellationToken) =>
        shell.RunAsync("git", ["status", "--porcelain"], workspaceRoot, Timeout, cancellationToken);

    /// <inheritdoc />
    public Task<ShellCommandResult> DiffAsync(string workspaceRoot, CancellationToken cancellationToken) =>
        shell.RunAsync("git", ["diff"], workspaceRoot, Timeout, cancellationToken);

    /// <inheritdoc />
    public Task<ShellCommandResult> LogAsync(string workspaceRoot, int maxEntries, CancellationToken cancellationToken) =>
        shell.RunAsync("git", ["log", "--oneline", "-n", maxEntries.ToString()], workspaceRoot, Timeout, cancellationToken);
}
```

### `src/MiniCode.Tools/GitTools.cs`

```csharp
using System.ComponentModel;
using MiniCode.Infrastructure;
using MiniCode.Workspace;

namespace MiniCode.Tools;

/// <summary>
/// The model-facing side of Git. <see cref="IGitClient"/> decides how a query
/// runs; this decides what the model gets told about it.
/// </summary>
public sealed class GitTools(IGitClient git, IWorkspace workspace)
{
    /// <summary>Reports modified, added and untracked files, or that there are none.</summary>
    public async Task<string> GitStatus(CancellationToken cancellationToken = default) =>
        Format(await git.StatusAsync(workspace.Root, cancellationToken), "Working tree clean.");

    /// <summary>Reports unstaged changes, or that there are none.</summary>
    public async Task<string> GitDiff(CancellationToken cancellationToken = default) =>
        Format(await git.DiffAsync(workspace.Root, cancellationToken), "No unstaged changes.");

    /// <summary>Reports the most recent commits, or that there are none.</summary>
    public async Task<string> GitLog(
        [Description("How many recent commits to show.")] int maxEntries = 10,
        CancellationToken cancellationToken = default) =>
        Format(await git.LogAsync(workspace.Root, maxEntries, cancellationToken), "No commits yet.");

    private static string Format(ShellCommandResult result, string whenEmpty) =>
        result.ExitCode != 0
            ? result.StandardError.Trim()
            : string.IsNullOrWhiteSpace(result.StandardOutput) ? whenEmpty : result.StandardOutput.TrimEnd();
}
```

`ToolCatalog` gains a sixth constructor parameter, `IGitClient git`, builds a `GitTools`, and registers `git_status`, `git_diff` and `git_log` through the same private `Intercept` as every other tool — three more entries, no new mechanism. `CodingAgentFactory` constructs `new GitClient(new ShellExecutor())` — a plain `ShellExecutor`, not the allow-listed one built for `run_command`, since `GitClient` has no arbitrary command for an allow list to filter — and passes it through. `AgentInstructions.Core` gains a few sentences: check `git_status` and `git_diff` before writing a change report rather than trusting memory of what was edited, use `git_log` for history, and a reminder that none of these three tools changes anything Git tracks.

## Walkthrough

1. **`IGitClient` takes a `workspaceRoot` string, not `IWorkspace`.** `MiniCode.Infrastructure` is a leaf project — it cannot reference `MiniCode.Workspace` — so the caller resolves the root and passes it in, exactly as `IShellExecutor.RunAsync` already does.
2. **Every argument list in `GitClient` is a C# array literal, never user input.** `run_command` builds its argument list from what the model supplies; `GitClient` builds its from the source code. That difference is the entire reason one needs an allow list and the other does not.
3. **`Format` checks `ExitCode`, not a try/catch.** A non-zero exit from `git` is Git successfully answering — "no repository here" — not a process failure. Only a process that could not start at all reaches `DirectToolInvoker`'s catch block.
4. **The three `whenEmpty` messages are each written for their own tool.** `git status --porcelain` and `git diff` both print nothing at all when there is nothing to report — the raw silence would read as a stalled tool, not a clean answer, so each result names the actual state Git is describing.
5. **`GitTools` takes the same two constructor arguments as `ShellTools`** — the client interface it wraps, and `IWorkspace` for the root — the same shape Module 11a already established for a tool that needs to know where it is running.

## Exercise

`GitDiff` returns the entire unstaged diff in one call, with no cap — unlike `ShellTools.RunCommand`, which truncates stdout at `ShellTools.MaxOutputLength` (Module 11a), a diff touching many files could be large enough to consume a meaningful slice of Module 8a's context budget in a single tool result. Add the same truncation to `GitDiff`, in `src/MiniCode.Tools/GitTools.cs`: cap the formatted result at `ShellTools.MaxOutputLength` characters, appending a notice naming how many characters were cut, the same way `ShellTools.Append` already does for command output. Acceptance criteria: a diff shorter than the cap is returned unchanged; a diff longer than the cap is cut to exactly the cap's length plus the notice; the notice states the number of characters truncated. No reference implementation ships in a later Module folder — this Module's own demonstration never produces a diff large enough to trigger it.

## Expected Output

All of the following is real output from this Module's code, calling `GitTools` — through the real `GitClient` and `ShellExecutor` — against a temporary Git repository with two commits and one uncommitted edit.

`git_status`, after editing a tracked file and adding an untracked one:

```text
 M CustomerService.cs
?? CustomerServiceTests.cs
```

`git_diff`, the same edit:

```text
diff --git a/CustomerService.cs b/CustomerService.cs
index baa88f6..b3b1fba 100644
--- a/CustomerService.cs
+++ b/CustomerService.cs
@@ -1,4 +1,4 @@
 public class CustomerService
 {
-    public Customer GetById(int id) => _repository.Find(id);
+    public Customer GetById(int id) => _cache.GetOrAdd(id, _repository.Find);
 }
```

`git_log(maxEntries: 5)`, newest first:

```text
3b62245 Add Program entry point
20bdef2 Add CustomerService
```

`git_status`, called against a directory that is not a Git repository at all — Git's own message, passed straight through:

```text
fatal: not a git repository (or any of the parent directories): .git
```

The Lab — asking MiniCode for a change report — needs a live `OPENAI_API_KEY`, since it is the model deciding to call these tools and phrase what it finds, not this lesson's code. What it produces is `git_status` and `git_diff` composed the way the instructions now direct: `git_status` above tells it one file changed and one is untracked, `git_diff` shows the change was replacing a direct repository call with a cache lookup, and the report it writes describes exactly that — sourced from these three tool calls, not from anything it remembers editing.
