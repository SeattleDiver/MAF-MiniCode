# Module 19 — Capstone Project (v1.0)

## Project Overview

MiniCode is finished. This Module adds no capability — it takes v0.4 into a repository that knows nothing about this course, gives it one sentence of English, and watches it inspect, plan, edit, build, test, diagnose, repair and report on its own. The version moves to 1.0 because the tool is done, not because anything new was written.

## Prerequisites

**Starting point:** open `Phase4-Capstone-DeveloperCli/`.

Everything from Phases 1–4. This is the course capstone; nothing follows it, and it feeds no Phase Capstone of its own.

## Setup

No new package, no new environment variable, no new project. You will need a small .NET solution to point MiniCode at that is *not* MiniCode — the sample below — and it must be a Git repository, because three of the eleven tools are Git tools and `git diff` is step fourteen.

## Core Concepts

**A capstone is a claim, and the claim is testable.** Every Module so far was verified against its own topic list. This one is verified against a sentence a developer would actually type. Either the tool completes the task without being told which files to open, or it does not.

**No new code is the result, not a shortcut.** Eleven tools have accumulated across eighteen Modules, each added for its own reason. The capstone asks whether they compose into an agent — and composition is something you demonstrate, not something you implement.

**Intelligence is centralised; capability is not.** There is one MAF agent. It decides. Every irreversible thing it can do lives in deterministic C#: a path boundary, a fixed command allow list, an approval prompt, an edit that refuses a stale fingerprint. The model chooses *what*; the C# decides *whether*.

**The fifteen steps are eleven tools.** The assignment reads as a long procedure. It is not a pipeline anyone wrote — it is what the model does with the tools it was handed, and every step maps onto something that already ships:

| Steps | What the agent does | Tool |
|---|---|---|
| 1 | Inspect the repository | `describe_repository`, `list_files` |
| 2–4 | Locate `CustomerService`, `Customer`, the tests | `search_files`, `read_file` |
| 5 | Decide the change | the plan, from `PlanAsync` |
| 6–7 | Modify production code, add a test | `edit_file`, `write_file` |
| 8–9 | Build; fix compilation errors | `run_command` `dotnet build` |
| 10–13 | Test, diagnose, correct, re-test | `run_command` `dotnet test`, `edit_file` |
| 14 | Inspect the change | `git_diff` |
| 15 | Explain what was done | the model's own words |

**Search before read is what makes step 2 possible.** The agent is never told where `CustomerService` lives. `search_files` returns `path:line: text`, so one call turns a type name into a file path without reading anything — which is the whole reason Module 8a put search ahead of read in the instructions.

**The repair loop is the interesting part.** Steps 10–13 are a cycle, not a line. `dotnet test` fails, `TestOutputParser` reduces the output to a summary plus the failing tests only, the model reads the assertion message, edits, runs again. Module 13 built that; the capstone is where it earns its place, because a first attempt that compiles and passes is the exception.

**What v1.0 is.** A coding agent you install once and run anywhere. It reads and writes inside a workspace boundary, runs five allow-listed commands, uses Git, plans before acting, asks before changing anything, answers eleven slash commands, writes a full stderr trace, and completes a real development task from one sentence.

**What v1.0 is not.** One agent, one session, one workspace — no sub-agents, no parallel tool calls, no persistence across restarts. Context is managed by search-and-summarise, not embeddings. The trace is text on stderr, with no structured events and no OpenTelemetry. And `TaskPlan`'s state machine — `WithState`, `Current`, `IsComplete`, `Revise` — is still called from nowhere in `src/`; the Phase 4 Capstone made driving it an exercise with no reference implementation, and shipping 1.0 does not change that. Twice-promised and undelivered would be worse than named and left open.

## The Code

The only change to MiniCode in this Module:

### `src/MiniCode.Cli/MiniCodeVersion.cs`

```csharp
namespace MiniCode.Cli;

/// <summary>
/// The version MiniCode reports at startup. It moves at a Capstone and nowhere
/// else: 0.1 was the read-only Repository Explorer, 0.2 was the Planning Agent,
/// 0.3 was the Autonomous Fixer, 0.4 was the Developer CLI that closed Phase 4,
/// and 1.0 is the course capstone — the same code, declared finished. It lives
/// in the CLI because the banner is terminal output. The package version in
/// MiniCode.Cli.csproj is a separate literal that moves with this one by
/// discipline — nothing reads either from the other.
/// </summary>
internal static class MiniCodeVersion
{
    public const string Current = "1.0";
}
```

`<Version>` in `src/MiniCode.Cli/MiniCode.Cli.csproj` moves with it, `0.4.0` to `1.0.0`. That is the entire diff against the Phase 4 Capstone.

The rest of the code in this Module is the sample repository MiniCode is pointed at. It is not part of MiniCode and does not live in this folder — create it anywhere outside the course repository, then `git init` and commit it. Two production files, a test project, and a solution file:

### Sample repository — `src/CustomerPortal.Core/Customer.cs`

```csharp
namespace CustomerPortal.Core;

/// <summary>A customer of the portal.</summary>
public sealed record Customer(string Name, string? Email);
```

### Sample repository — `src/CustomerPortal.Core/CustomerService.cs`

```csharp
namespace CustomerPortal.Core;

/// <summary>Creates and stores customers.</summary>
public sealed class CustomerService
{
    private readonly List<Customer> _customers = [];

    /// <summary>The customers created so far.</summary>
    public IReadOnlyList<Customer> Customers => _customers;

    /// <summary>Creates a customer and adds it to the store.</summary>
    public Customer Create(string name, string? email)
    {
        var customer = new Customer(name, email);
        _customers.Add(customer);
        return customer;
    }
}
```

Alongside those, a `tests/CustomerPortal.Tests` project referencing `CustomerPortal.Core`, holding one passing check that creates a customer with an email and asserts it was stored — enough that `dotnet test` has something to run before MiniCode touches anything. A `CustomerPortal.slnx` covers both projects.

Two details the sample needs or `/test` will not reproduce the captures below. The project uses `xunit.v3` 4.0.0 with `<OutputType>Exe</OutputType>` and no `Microsoft.NET.Test.Sdk`; and the sample's root needs a `global.json` containing `{ "test": { "runner": "Microsoft.Testing.Platform" } }`, because without it the .NET 10 SDK refuses the run with an MTP-versus-VSTest error rather than reporting a result.

## Walkthrough

1. **The version bump is the whole code change.** Two literals, in two files, that nothing reads from each other. Keeping them in step is discipline, and this is the last time either moves.
2. **`Create` accepts a null email.** That is the defect the assignment describes, and it is a defect the compiler is happy with — `string? email` says so explicitly. A tool that only fixed compiler errors would find nothing here.
3. **The sample is deliberately small and deliberately ordinary.** Two projects, a records-and-service shape, one existing test. Nothing about it is tuned for the agent: the point of the capstone is a repository MiniCode has never seen and was not designed against.
4. **`git init` matters.** Step 14 is `git diff`, and `/diff`, `/review` and `/commit` all go through `GitClient`. Against a directory that is not a repository they return Git's own error, which is correct but is not the demonstration.
5. **Run MiniCode from the sample's directory**, not from the course repository. The workspace root is the current directory unless you pass one, and every path the model supplies is resolved against it by `IWorkspace` before anything opens a file.

## Exercise

**One: the second scenario in the assignment.** Commit the completed change, then break it deliberately — invert the validation, or delete the null check while leaving the test in place — and give MiniCode nothing but:

```text
> The CustomerService tests are failing. Find the problem and fix it.
```

Acceptance criteria: the agent runs `dotnet test` without being asked to; it names the failing test and the file it is asserting against before editing anything; it edits production code rather than deleting or weakening the test; `dotnet test` passes afterwards; and `/diff` shows a change confined to `CustomerService.cs`. If it edits the test instead of the code, that is a finding about the instructions in `AgentInstructions.cs`, not a bug — say what you would add to them.

**Two: drive the plan.** Still open, still unimplemented, still the most valuable thing left in the solution. Make `RunStreamingAsync` advance the plan it was given — mark a step `Complete` as the model finishes it, and use `TaskPlan.Revise` when the model's actual course diverges. Acceptance criteria: `/status` after a turn shows which steps completed; a plan whose steps all complete reports `IsComplete`; revising never renumbers or removes a step already shown to the operator; `TaskPlan` stays immutable, so each change produces a new instance. The acceptance criteria are the specification — there is no later Module, and no reference implementation exists.

## Expected Output

Be exact about what produced these. Every block below is real terminal output from this Module's own folder, run against the sample repository described above. What is **not** here is the model's own narration — the `Planning:` lines, the tool-call announcements and the closing explanation — because producing those requires a live OpenAI call, and no request was sent to OpenAI for any capture on this page. The captures are the deterministic half: the banner, the eleven-command help, and the build/test/diff cycle that steps 8–14 drive, each produced by MiniCode's own tools rather than by running `dotnet` directly.

The banner, from the sample repository's directory:

```text
MiniCode v1.0
Workspace: C:\Users\...\CustomerPortal
MiniCode. Type 'exit' to quit, or /help for commands.

> /help    Show this list
/status  Show the context budget
/files   List files in the workspace
/diff    Show unstaged changes
/build   Run dotnet build
/test    Run dotnet test
/commit  Commit tracked changes, e.g. /commit fix the bug
/plan    Plan a request without acting on it, e.g. /plan add caching
/review  Review the current unstaged changes
/clear   Discard the conversation so far
/exit    Quit MiniCode
```

Step 10, the moment the repair loop starts — the new test is in place and the validation is not. `TestOutputParser` has reduced a full `dotnet test` run to the summary and the one failing test; the passing test is not named:

```text
> /test
Exit code 2
Test run summary: Failed!
total: 2
failed: 1
succeeded: 1

failed CustomerPortal.Tests.CustomerServiceTests.Create_WithoutEmail_Throws (2ms)
  Assert.Throws() Failure: No exception was thrown
  Expected: typeof(System.ArgumentException)
  at CustomerPortal.Tests.CustomerServiceTests.Create_WithoutEmail_Throws() in ...\CustomerServiceTests.cs:24
```

That assertion message, the expected type and the line number are everything the model needs for steps 11 and 12. It never sees the passing test.

Steps 8 and 13, after the guard clause lands — build clean, both tests green:

```text
> /build
Exit code 0
  Determining projects to restore...
  All projects are up-to-date for restore.
  CustomerPortal.Core -> C:\Users\...\CustomerPortal\src\CustomerPortal.Core\bin\Debug\net10.0\CustomerPortal.Core.dll
  CustomerPortal.Tests -> C:\Users\...\CustomerPortal\tests\CustomerPortal.Tests\bin\Debug\net10.0\CustomerPortal.Tests.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

> /test
Exit code 0
Test run summary: Passed!
total: 2
failed: 0
succeeded: 2
```

Step 14, `/diff`. The real output has two hunks; the second is on the test file and is omitted here, because no test code appears in this course:

```text
> /diff
diff --git a/src/CustomerPortal.Core/CustomerService.cs b/src/CustomerPortal.Core/CustomerService.cs
index 88d6052..e458327 100644
--- a/src/CustomerPortal.Core/CustomerService.cs
+++ b/src/CustomerPortal.Core/CustomerService.cs
@@ -11,6 +11,11 @@ public sealed class CustomerService
     /// <summary>Creates a customer and adds it to the store.</summary>
     public Customer Create(string name, string? email)
     {
+        if (string.IsNullOrWhiteSpace(email))
+        {
+            throw new ArgumentException("A customer requires an email address.", nameof(email));
+        }
+
         var customer = new Customer(name, email);
         _customers.Add(customer);
         return customer;
```

And the same session captured with `2> trace.log`, which is the record of what actually ran:

```text
[Tool] run_command
[Result] Exit code 0 (3348ms)
[Tool] run_command
[Result] Exit code 0 (4685ms)
[Tool] git_diff
[Result] diff --git a/src/CustomerPortal.Core/CustomerService.cs ... (103ms)
```

That is MiniCode v1.0: 57 C# files across five projects, 2,346 lines, eleven tools, eleven slash commands, one agent — finding a defect in a repository it had never seen, fixing it, and proving the fix.
