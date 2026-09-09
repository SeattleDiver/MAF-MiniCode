# Module 13 — Automated Testing and Self-Correction

## Project Overview

`dotnet test` has run through `run_command` since Module 11a like any other command — raw stdout, truncated at 4,000 characters. That was fine for a build. A test run buries the one line that matters — which test failed, and why — inside output that is mostly the runner announcing itself. This Module is what makes that line easy to find, and closes the loop: fix, rerun, confirm.

## Prerequisites

**Starting point:** open `Module12-TheAutonomousCodingLoop/`.

Module 11a's `run_command` and `ShellCommandResult`, and Module 12's loop and its addition to `AgentInstructions.Core`. Module 6's `describe_repository` already marks a project `[test project]` — that question was answered before this Module exists; this one is about what happens after the model decides to run one.

Last Module of Phase 3 — the Capstone follows. Ships as part of **Phase 3 Capstone — Autonomous Fixer (v0.3)**.

## Setup

Nothing new.

## Core Concepts

```text
Modify Code → dotnet build → dotnet test → Tests Pass?
                                              /      \
                                            NO        YES
                                            │           │
                                       Diagnose       Finish
                                            │
                                         Modify ──────► Test Again
```

**Unit-test discovery already has an answer; this Module is not it.** Module 6 marks a project `[test project]` the first time `describe_repository` runs. What was missing was never "does the model know tests exist" — it was "can it read what running them produced."

**The runner is quiet on success and loud on failure — verified directly, not assumed.** A passing run prints one line: `Probe.dll (net10.0|x64) passed (596ms)`. A failing run itemizes every failure by name, with its assertion and a stack trace. Parsing does not have to find the failures; the runner already decided which lines matter. It only has to keep those and drop the rest.

**Exit codes extend Module 11a's topic, and the numbers are not the ones a build uses.** Captured directly: `0` is a clean pass, `2` means one or more tests failed, and `5` means the test host never ran anything at all — a configuration problem, not a red test.

**`--nologo` is that configuration problem, and it is real.** `dotnet test --nologo` on this SDK reports `Zero tests ran` with exit code `5` — confirmed against this Module's own toolchain. A model that picked up the habit from `dotnet build --nologo` in Module 11a's own lesson would silently stop testing anything. Instructions forbid it outright now.

**The first stack frame is the test; the rest is the framework that called it.** `Assert.Equal() Failure`, `Expected`, `Actual`, then one `at` line pointing at the test method and its source line. Everything after that is `System.Reflection` invoking the test — real, but not worth a token.

**A summary that cannot be built is not the same as an empty one.** A syntax error inside the test project never reaches the test host — there is no `Test run summary:` line to find. Falling back to the raw compiler output here is the same instinct as Module 5b's truncation notices: silence reads as "nothing happened," which is worse than showing something unfiltered.

**Regression detection ships as an instruction here, not a comparison.** The parser reports what just happened; it keeps no memory of the run before it. Treating a newly broken test as seriously as the one being fixed — by rerunning the *whole* suite, not just the target — is behavior asked of the model, the same way Module 12 asked it to rerun a command before declaring success.

## The Code

### `src/MiniCode.Tools/TestOutputParser.cs`

```csharp
using System.Linq;
using System.Text;

namespace MiniCode.Tools;

/// <summary>
/// Condenses dotnet test output for Microsoft.Testing.Platform runners.
/// Passing tests are not itemized by the runner itself, only failing ones
/// are — so this keeps the summary counts and each failure's message and
/// source location, and drops the rest.
/// </summary>
public static class TestOutputParser
{
    private const int MaxFailures = 5;

    /// <summary>Extracts the summary line and up to five failures from raw output.</summary>
    public static string Summarize(string output)
    {
        string[] lines = output.ReplaceLineEndings("\n").Split('\n');
        var text = new StringBuilder();

        foreach (string line in lines)
        {
            string trimmed = line.TrimStart();
            if (line.StartsWith("Test run summary:") || trimmed.StartsWith("total:")
                || trimmed.StartsWith("failed:") || trimmed.StartsWith("succeeded:") || trimmed.StartsWith("error:"))
            {
                text.AppendLine(trimmed);
            }
        }

        int total = lines.Count(l => l.StartsWith("failed "));
        int shown = 0;
        for (int i = 0; i < lines.Length && shown < MaxFailures; i++)
        {
            if (!lines[i].StartsWith("failed "))
            {
                continue;
            }

            text.AppendLine().AppendLine(lines[i]);
            shown++;

            int j = i + 1;
            while (j < lines.Length && lines[j].StartsWith(' '))
            {
                string detail = lines[j].Trim();
                j++;
                if (detail.StartsWith("from ", StringComparison.Ordinal))
                {
                    continue;
                }

                text.AppendLine("  " + detail);
                if (detail.StartsWith("at ", StringComparison.Ordinal))
                {
                    break; // the first frame is the test itself; the rest is framework noise
                }
            }
        }

        if (total > shown)
        {
            text.AppendLine().AppendLine($"... {total - shown} more failing tests not shown.");
        }

        return text.ToString();
    }
}
```

Where `ShellTools.RunCommand` uses it, from `src/MiniCode.Tools/ShellTools.cs`:

```csharp
    /// <summary>Runs a command in the workspace root and reports what happened.</summary>
    public async Task<string> RunCommand(
        [Description("The program to run, e.g. \"dotnet\". Not passed through a shell.")] string command,
        [Description("Arguments to pass, e.g. [\"build\"]. Omit for none.")] string[]? arguments = null,
        [Description("Seconds to allow before the process is killed.")] int timeoutSeconds = 120,
        CancellationToken cancellationToken = default)
    {
        string[] args = arguments ?? [];
        ShellCommandResult result = await shell.RunAsync(
            command, args, workspace.Root, TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);

        if (result.TimedOut)
        {
            return $"'{command}' did not finish within {timeoutSeconds}s and was killed.";
        }

        var text = new StringBuilder($"Exit code {result.ExitCode}");
        string? summary = IsTestRun(command, args) ? TestOutputParser.Summarize(result.StandardOutput) : null;
        if (!string.IsNullOrWhiteSpace(summary))
        {
            // A run that never reached the test host — a compile error — has no
            // summary to extract; the raw output is the only useful thing to show.
            text.Append(Environment.NewLine).Append(summary);
        }
        else
        {
            Append(text, result.StandardOutput);
        }

        Append(text, result.StandardError, "stderr:");
        return text.ToString();
    }

    private static bool IsTestRun(string command, string[] arguments) =>
        command.Equals("dotnet", StringComparison.OrdinalIgnoreCase)
        && arguments is [var sub, ..] && sub.Equals("test", StringComparison.OrdinalIgnoreCase);
```

And the new third of `Core`, from `src/MiniCode.Agent/AgentInstructions.cs` — `Floor` and `Compose` are unchanged:

```csharp
        + "twice, stop and explain what you tried instead of repeating it. Never pass "
        + "--nologo to dotnet test: on this SDK it can report \"Zero tests ran\" with "
        + "exit code 5 instead of running anything. After fixing a failing test, run "
        + "the full test suite again, not just the one that was failing — a change "
        + "that breaks a different test is not a fix, and a newly failing test "
        + "deserves the same attention as the one you started with.";
```

`run_command`'s description in `ToolCatalog` gains one clause: *"For dotnet test, the summary and failing tests are reported; passing tests are not listed by name."*

## Walkthrough

1. **`IsTestRun` checks the subcommand, not just the program** — the same shape as Module 11b's allow-list matching, spent here on a parsing decision instead of a permission one.
2. **`string.IsNullOrWhiteSpace(summary)` covers two cases at once**: not a test run at all (`summary` is `null`), and a test run whose output had nothing to extract (`summary` is empty). A compile error inside the test project is the second case, and falls through to the same raw output a build gets.
3. **No regex anywhere in the parser** — line-prefix and substring checks only, the same style as every parser this course has written since `FileSystemService`.
4. **The inner loop prints every detail line until it prints one starting with `at `, then stops.** That first stack frame is the test method; everything after it is `System.Reflection` invoking the test, which the model has never needed to see.
5. **The failure cap counts before it truncates.** `total` is computed once, up front, so the `... N more failing tests not shown.` notice is exact even though the loop itself stops as soon as it has shown five.
6. **`AgentInstructions.Core` grows for the third time — Module 7, then 12, now 13.** No tool's shape changed this Module; only what the model is told to do with output it can now actually read.

## Exercise

**Relativize the paths in `run_command`'s output.** The parser hands back lines like `at CalculatorTests.SubtractsTwoNumbers() in C:\Repo\CalculatorTests.cs:9` — an absolute path, because that is what the test host prints. Every instruction in `AgentInstructions.Core` says paths are relative to the workspace root, and `IWorkspace.ValidatePath` refuses a rooted path outright, so a model that copies that path straight into `read_file` gets refused with `NotRelative` and has to recover. It does recover — Module 5a's invoker still turns the refusal into a sentence it can read — but it is friction on what should be the happy path, and Module 11a's own compiler-error output has the identical shape.

In `ShellTools.RunCommand`, after getting `result`, replace occurrences of `workspace.Root` (plus its trailing separator) with nothing in both `result.StandardOutput` and `result.StandardError` before handing them to the parser or to `Append`, and normalize backslashes to forward slashes in what remains. Acceptance criteria: a path inside the workspace root appears relative, with forward slashes, in both build and test output; a path outside the workspace root (a framework assembly, say) is left exactly as printed; running this against output that contains no workspace path at all changes nothing. This belongs in `src/MiniCode.Tools/ShellTools.cs`. No later Module needs this — Module 14's Git tools format their own paths independently.

## Expected Output

All of the following is real output from this Module's code, calling `ShellTools.RunCommand` — through the real `AllowListedShellExecutor` and `CommandAllowList` — against a temporary xunit.v3 test project with one failing test.

The failing run:

```text
Exit code 2
Test run summary: Failed!
total: 2
failed: 1
succeeded: 1

failed CalculatorTests.SubtractsTwoNumbers (17ms)
  Assert.Equal() Failure: Values differ
  Expected: 1
  Actual:   2
  at CalculatorTests.SubtractsTwoNumbers() in C:\...\CalculatorTests.cs:9
```

The mistake this Module exists to prevent, captured for real:

```text
Exit code 5
Test run summary: Zero tests ran
error: 1
total: 0
failed: 0
succeeded: 0
```

And after fixing `Calculator.Subtract`, the same call:

```text
Exit code 0
Test run summary: Passed!
total: 2
failed: 0
succeeded: 2
```

The Lab itself — handing MiniCode a failing test and asking it to fix the underlying code — needs a live `OPENAI_API_KEY`, since it is the model deciding what to search for and what to edit, not this lesson's code. What it produces is this Module's tool call composed with Module 10's `edit_file` and Module 12's loop: the condensed failure above tells it which method and which line, `search_files` or a direct `read_file` on `CalculatorTests.cs:9` finds `Calculator.Subtract`, `edit_file` corrects it, and `run_command` runs again to confirm — `Exit code 0` where it was `2`.
