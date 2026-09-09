# Module 16a — Developer CLI: Dispatch and Informational Commands

## Project Overview

Every prior Module has treated the operator's input as one thing: a request for the model. This Module gives MiniCode a second channel — a line starting with `/` is a command, run deterministically, no model call involved. `/help`, `/status`, `/files`, `/diff`, `/build`, `/test`, `/commit`, `/clear`, `/exit`. `/plan` and `/review` — the two commands that need genuinely new agent logic rather than a wire-through — are Module 16b.

## Prerequisites

**Starting point:** open `Module15-HumanApprovalAndSafety/`.

Every tool built since Module 4, reached through Module 5a's interception seam. Module 15's `ApprovalInvoker` and Module 11b's `CommandAllowList` — a slash command reaches the exact same policy the model does, not a shortcut around it.

Third Module of Phase 4. Ships as part of **Phase 4 Capstone — Developer CLI (v0.4)**.

## Setup

Nothing to install. No new package.

## Core Concepts

**A slash command bypasses the model, not its policies.** `/build` does not ask gpt-4.1-mini to call `run_command` — `ConsoleChatLoop` calls it directly, through the identical `AIFunction` the model would have called. Same allow list, same approval, same loop guard.

**`ICodingAgent` grows by exactly two members, and stays string-only.** `InvokeToolAsync` takes a tool name and a plain `IReadOnlyDictionary<string, object?>` and returns a `string` — no `AIFunction`, no `AIFunctionArguments` crosses into `MiniCode.Cli`, honoring the interface's own founding rule.

**Slash commands and the model share one dictionary of tools, built once.** `CodingAgent` indexes `ToolCatalog`'s tools by name — the same `InterceptedFunction` instances `ChatClientAgent` already holds. There is no second copy of any tool, so there is no way for a slash command to see a different `run_command` than the model does.

**`git commit` needed one more allow-list entry to be reachable at all.** Module 11b's list matches program plus first argument; `/commit` is the first thing in the course to actually ask for `git`, so `"git commit"` joins the four `dotnet` entries — still approval-gated exactly like every other non-build, non-test `run_command`.

**Silence during a tool call was always a gap, not a feature.** Since Module 5a, a tool call mid-turn showed nothing — `AgentResponseUpdate.ToString()` has always been empty for one. `RunStreamingAsync` now shows the call itself in that gap instead of nothing.

**A slash command still resets the loop guard.** `InvokeToolAsync` calls the same `Reset()` `RunStreamingAsync` already calls at the start of a turn — a `/build` typed between two chat turns does not eat into either turn's 20-call budget.

**Ctrl+C ends a turn, not the process.** `Console.CancelKeyPress` cancels a token scoped to whatever is running right now; the loop catches the cancellation and asks for the next line instead of the process exiting.

**One more catch keeps a bad turn from ending the session.** Any other exception during a turn now prints and the loop continues, instead of unwinding out of `Main` and killing MiniCode over one failed call.

## The Code

### `src/MiniCode.Cli/SlashCommand.cs`

```csharp
namespace MiniCode.Cli;

/// <summary>One "/name rest of line" command, parsed from what the operator typed.</summary>
public sealed record SlashCommand(string Name, string? Argument)
{
    /// <summary>Parses input starting with "/", or null when it does not.</summary>
    public static SlashCommand? TryParse(string input)
    {
        if (!input.StartsWith('/'))
        {
            return null;
        }

        string body = input[1..].Trim();
        int space = body.IndexOf(' ');
        return space < 0
            ? new SlashCommand(body.ToLowerInvariant(), null)
            : new SlashCommand(body[..space].ToLowerInvariant(), body[(space + 1)..].Trim());
    }
}
```

### `src/MiniCode.Agent/ICodingAgent.cs`

```csharp
namespace MiniCode.Agent;

/// <summary>
/// The one thing the terminal is allowed to know about the agent. Deliberately
/// expressed in <see cref="string"/> only, so no framework type crosses into
/// <c>MiniCode.Cli</c>.
/// </summary>
public interface ICodingAgent
{
    /// <summary>What the session has spent so far, as a line for the operator.</summary>
    string ContextSummary { get; }

    /// <summary>Turns a request into an inspectable plan. Reads nothing, changes nothing.</summary>
    Task<TaskPlan> PlanAsync(string request, CancellationToken cancellationToken = default);

    /// <summary>Streams the agent's answer to a request, fragment by fragment.</summary>
    IAsyncEnumerable<string> RunStreamingAsync(
        string request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs one named tool directly, through the same approval and loop-guard
    /// policy the model's own calls pass through. For a slash command, not chat.
    /// </summary>
    Task<string> InvokeToolAsync(
        string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default);

    /// <summary>Discards the conversation so far. The workspace itself is untouched.</summary>
    void ClearSession();
}
```

The implementation, from `src/MiniCode.Agent/CodingAgent.cs`:

```csharp
    /// <inheritdoc />
    public async Task<string> InvokeToolAsync(
        string toolName, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        if (!_tools.TryGetValue(toolName, out AIFunction? function))
        {
            return $"No such tool: {toolName}.";
        }

        _guard.Reset();
        var boundArguments = new AIFunctionArguments();
        foreach ((string key, object? value) in arguments)
        {
            boundArguments[key] = value;
        }

        object? result = await function.InvokeAsync(boundArguments, cancellationToken);
        return result?.ToString() ?? string.Empty;
    }
```

Tool activity display — the whole of it, also in `CodingAgent.cs`:

```csharp
    private static string DescribeToolCalls(AgentResponseUpdate update) =>
        string.Concat(update.Contents.OfType<FunctionCallContent>()
            .Select(call => $"{Environment.NewLine}→ {call.Name}({JsonSerializer.Serialize(call.Arguments)}){Environment.NewLine}"));
```

The one changed line in `src/MiniCode.Infrastructure/CommandAllowList.cs`:

```csharp
    private static readonly string[] Allowed =
        ["dotnet restore", "dotnet build", "dotnet test", "dotnet format", "git commit"];
```

The dispatcher itself, from `src/MiniCode.Cli/ConsoleChatLoop.cs`:

```csharp
    /// <summary>Runs one command. Returns false only for /exit, to end the loop.</summary>
    private async Task<bool> DispatchAsync(SlashCommand command, CancellationToken cancellationToken)
    {
        switch (command.Name)
        {
            case "help":
                Console.WriteLine(HelpText);
                return true;
            case "status":
                Console.WriteLine(agent.ContextSummary);
                return true;
            case "files":
                Console.WriteLine(await agent.InvokeToolAsync("list_files", new Dictionary<string, object?>(), cancellationToken));
                return true;
            case "diff":
                Console.WriteLine(await agent.InvokeToolAsync("git_diff", new Dictionary<string, object?>(), cancellationToken));
                return true;
            case "build":
                Console.WriteLine(await RunShellAsync("dotnet", ["build"], cancellationToken));
                return true;
            case "test":
                Console.WriteLine(await RunShellAsync("dotnet", ["test"], cancellationToken));
                return true;
            case "commit":
                string message = string.IsNullOrWhiteSpace(command.Argument) ? "MiniCode commit" : command.Argument;
                Console.WriteLine(await RunShellAsync("git", ["commit", "-a", "-m", message], cancellationToken));
                return true;
            case "clear":
                agent.ClearSession();
                Console.WriteLine("Session cleared.");
                return true;
            case "exit":
                return false;
            default:
                Console.WriteLine($"Unknown command: /{command.Name}. Type /help for a list.");
                return true;
        }
    }
```

`CodingAgent`'s constructor gains an `IReadOnlyList<AITool> tools` parameter and builds `_tools = tools.OfType<AIFunction>().ToDictionary(t => t.Name)`; `CodingAgentFactory` passes `catalog.GetTools()` through unchanged from where it already built it for `ChatClientAgent`. `RunStreamingAsync` calls `DescribeToolCalls(update)` in place of an empty fragment. `ConsoleChatLoop.RunAsync` wraps each turn in `try`/`catch (OperationCanceledException)`/`catch (Exception)`, and a `Console.CancelKeyPress` handler cancels a per-turn `CancellationTokenSource` instead of letting Ctrl+C kill the process. `HelpText` and a `RunShellAsync` helper round out the file; neither is shown here since neither does anything the switch above doesn't already explain.

## Walkthrough

1. **`SlashCommand.TryParse` returns `null` for anything not starting with `/`**, so `ConsoleChatLoop` can pattern-match once — `is { } command` — and fall through to the existing chat path otherwise.
2. **`_tools` is built once, in the constructor, from the exact list `ChatClientAgent` was given.** A slash command's `run_command` and the model's `run_command` are `TryGetValue` calls into the same dictionary, not two different objects that happen to share a name.
3. **`InvokeToolAsync` calls `function.InvokeAsync` on an `InterceptedFunction`,** so it runs the whole chain — `LoopGuardInvoker` → `ApprovalInvoker` → `DirectToolInvoker` → the real tool — exactly as if the model had called it.
4. **`/commit` builds `git commit -a -m <message>`,** staging every already-tracked modification but no new untracked file — the smallest thing that satisfies the syllabus's example without adding a `git add` step nothing asked for.
5. **`DescribeToolCalls` only reads `FunctionCallContent`, never `FunctionResultContent`.** A tool's result can be thousands of characters of build or test output; showing the call is enough to tell the operator something is happening without flooding the screen with its answer.
6. **The `Allowed` array is still a flat list of `"program subcommand"` strings.** Adding Git required no new matching logic, only a new entry — `CommandAllowList`'s shape from Module 11b already generalized past `dotnet`.
7. **A `catch (Exception)` this broad is deliberately the last resort of one turn, not a substitute for validation elsewhere.** Every tool in this solution already follows the never-throw discipline from Module 5a; this catch exists for whatever that discipline does not reach — a network fault talking to OpenAI, for instance — not as a place to route expected outcomes.

## Exercise

Wire a `/log` command to `git_log`, in `src/MiniCode.Cli/ConsoleChatLoop.cs`, following the exact shape `/diff` already uses. `command.Argument`, when present, is how many entries to show; `git_log`'s own tool already defaults to ten when the argument is omitted. Acceptance criteria: `/log` with no argument shows the default ten; `/log 3` shows three; a non-numeric argument (`/log all`) does not throw and falls back to the tool's default rather than crashing the turn. No reference implementation ships in a later Module folder — the course itself only ever calls `git_log` through the model.

## Expected Output

All of the following is real output from this Module's own code — `SlashCommand`, `ToolCatalog`'s real tools, a real temporary Git repository, and a scripted `IApprovalPrompter` standing in for a person's Y/N/A answer. No model call is involved, since dispatch is deterministic.

Parsing:

```text
/status                             -> Name=status, Argument=null
/commit fix the off-by-one bug      -> Name=commit, Argument=fix the off-by-one bug
build the project                   -> null (not a command)
```

`/files` and `/diff`, against a repository with one uncommitted edit:

```text
CustomerService.cs
```

```text
diff --git a/CustomerService.cs b/CustomerService.cs
index 6d9dd20..1f04f10 100644
--- a/CustomerService.cs
+++ b/CustomerService.cs
@@ -1 +1 @@
-public class CustomerService { }
+public class CustomerService { public int Version => 2; }
```

`/commit Bump CustomerService to v2` — approval fires first, since `git commit` is not `dotnet build` or `dotnet test`:

```text
MiniCode wants to run:
run_command({"command":"git","arguments":["commit","-a","-m","Bump CustomerService to v2"]})
Allow? [Y] Yes  [N] No  [A] Always allow for this session: (scripted: Approve)
Exit code 0
[master 5f67412] Bump CustomerService to v2
 1 file changed, 1 insertion(+), 1 deletion(-)
```

`/diff` again, immediately after — the commit actually landed:

```text
No unstaged changes.
```

Tool activity display, for the update a `read_file` call produces mid-turn:

```text
update.ToString() = []
Shown to operator:
→ read_file({"path":"src/CustomerService.cs"})
```

Cancellation, error presentation, and the Y/N/A prompt in `/commit` itself all need a real terminal and a live `OPENAI_API_KEY` to demonstrate as MiniCode actually runs — a keystroke and a process signal are not things this lesson's code can produce. What reaches them is exactly the sequence captured above: the same `ApprovalInvoker` prompt, the same tool results, driven by `ConsoleChatLoop` instead of a harness.
