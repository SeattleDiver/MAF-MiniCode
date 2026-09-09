# Module 16b — Developer CLI: Plan and Review Modes

## Project Overview

Module 16a gave MiniCode a dispatcher and wired the mechanical commands straight to existing tools. This Module adds the two commands that need real new logic: `/plan`, which shows a plan without ever starting the loop that follows one, and `/review`, a second no-tools model call — built exactly like Module 9's `Planner` — that reads a Git diff for the five things the syllabus names.

## Prerequisites

**Starting point:** open `Module16a-DispatchAndInformationalCommands/`.

Module 9's `Planner`/`TaskPlan`, and Module 14's `git_diff`. The dispatcher and `SlashCommand` parsing from Module 16a — `/plan` and `/review` are two more `case` arms, not a second mechanism.

Fourth Module of Phase 4. Ships as part of **Phase 4 Capstone — Developer CLI (v0.4)**.

## Setup

Nothing to install. No new package.

## Core Concepts

**`/plan` reuses `PlanAsync` for what it already was — a read-only preview.** Every chat turn has planned before acting since Module 9; `/plan` is the same call, invoked on its own, with nothing after it. No loop runs, so there is nothing to warn about — `/plan`'s output has no "no files modified" notice, because that notice exists to describe a run that, here, never happens.

**`/review` is `Planner`'s shape, aimed at a different question.** `Reviewer` makes one model call with `Tools = null` — it cannot read a file, run a command, or edit anything, structurally, the same guarantee `Planner` has had since Module 9. Where `Planner` turns a request into steps, `Reviewer` turns a diff into a report.

**Review's input is fetched, not typed.** The syllabus's own example is bare — `> /review`, no argument — because there is only one diff to review: whatever `git_diff` currently returns. `CodingAgent.ReviewAsync` calls the same `git_diff` tool `/diff` already calls, then hands the result to `Reviewer`.

**An empty diff never reaches the model.** `Reviewer.ReviewAsync` checks for one before building a single `ChatMessage` — reviewing nothing is answered in code, not by spending a call to be told there was nothing to say.

**Both modes now charge the budget honestly.** `ReviewAsync` estimates the diff and the report the same way `PlanAsync` already estimates the request and the rendered plan — a second no-tools call is still a call.

## The Code

### `src/MiniCode.Agent/Reviewer.cs`

```csharp
using Microsoft.Extensions.AI;

namespace MiniCode.Agent;

/// <summary>
/// Reviews a Git diff for the things the syllabus names. Built the same way as
/// <see cref="Planner"/>: the call is made with no tools at all, so a review can
/// only read the diff it is handed — never the repository, never a file.
/// </summary>
public sealed class Reviewer(IChatClient client)
{
    private const string Prompt =
        "You are reviewing a Git diff, not a whole repository. Read only the diff below "
        + "and report, briefly: correctness problems, regressions, security issues, "
        + "changes that look unnecessary, and tests that appear to be missing. If the "
        + "diff raises none of these, say so plainly instead of inventing a concern.";

    /// <summary>Reviews the given diff. Empty input is reported without a model call.</summary>
    public async Task<string> ReviewAsync(string diff, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(diff))
        {
            return "Nothing to review — there are no unstaged changes.";
        }

        ChatResponse response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.System, Prompt), new ChatMessage(ChatRole.User, diff)],
            new ChatOptions { Tools = null, ToolMode = ChatToolMode.None },
            cancellationToken);

        return response.Text;
    }
}
```

`ICodingAgent` gains one member alongside `InvokeToolAsync` and `ClearSession`:

```csharp
    /// <summary>Reviews the repository's current unstaged changes. Calls no tool that writes.</summary>
    Task<string> ReviewAsync(CancellationToken cancellationToken = default);
```

The implementation, from `src/MiniCode.Agent/CodingAgent.cs`:

```csharp
    /// <inheritdoc />
    public async Task<string> ReviewAsync(CancellationToken cancellationToken = default)
    {
        string diff = await InvokeToolAsync("git_diff", new Dictionary<string, object?>(), cancellationToken);
        string report = await _reviewer.ReviewAsync(diff, cancellationToken);

        // Review is a model call like any other — see PlanAsync above.
        _usedTokens += TokenEstimator.Estimate(diff) + TokenEstimator.Estimate(report);
        return report;
    }
```

The two new commands, from `src/MiniCode.Cli/ConsoleChatLoop.cs`:

```csharp
            case "plan":
                if (string.IsNullOrWhiteSpace(command.Argument))
                {
                    Console.WriteLine("Usage: /plan <request>");
                    return true;
                }

                TaskPlan plan = await agent.PlanAsync(command.Argument, cancellationToken);
                Console.WriteLine(plan.IsEmpty ? "No plan produced." : plan.Render());
                return true;
            case "review":
                Console.WriteLine(await agent.ReviewAsync(cancellationToken));
                return true;
```

`CodingAgent`'s constructor gains a `Reviewer reviewer` parameter, stored alongside `_planner`; `CodingAgentFactory` builds it as `new Reviewer(chatClient)` — the same `chatClient` `Planner` already uses, no second connection. `HelpText` gains two lines describing `/plan` and `/review`.

## Walkthrough

1. **`/plan`'s handler is the only `case` that validates its argument first.** Every other command either needs none or tolerates a missing one (`/commit` falls back to a default message); a plan with no request to plan for is simply an error, so it says so and returns without calling anything.
2. **`ReviewAsync` calls `InvokeToolAsync` internally, the same method `/diff` calls from the dispatcher.** There is exactly one path to `git_diff`'s result; `/review` does not re-implement fetching a diff, it reuses the one Module 16a already built.
3. **`Reviewer` takes only an `IChatClient`, not conventions like `Planner` does.** A repository's own style rules shape what a plan should do; they have no obvious bearing on whether a diff is correct, which is why this Module does not thread `AGENTS.md` through review too.
4. **The empty-diff guard is an `if`, not a special case inside the prompt.** Asking the model to say "nothing to review" when handed nothing would still cost a call; checking first costs nothing.
5. **`plan.Render()` is Module 9's method, unchanged.** `/plan`'s only new behavior is calling `PlanAsync` without also calling `RunStreamingAsync` afterward — the rendering was never the part that needed building.

## Exercise

`/review` only ever reviews `git_diff` — the unstaged changes. Add a second form, `/review staged`, that reviews `git diff --cached` instead. This needs a new method on `IGitClient` (`src/MiniCode.Infrastructure/IGitClient.cs` and `GitClient.cs`) alongside the existing `DiffAsync`, a matching `git_diff_staged` tool in `GitTools`/`ToolCatalog`, and a check on `command.Argument` in the `"review"` case. Acceptance criteria: `/review` with no argument behaves exactly as it does today; `/review staged` reviews staged changes instead; an argument that is neither empty nor `"staged"` is reported as a usage error rather than silently falling back to one or the other. No reference implementation ships in a later Module folder.

## Expected Output

The following is real, deterministic output from this Module's own code — no model call for the first case, and a plan built directly rather than through a live model for the second, since `Reviewer` and `TaskPlan.Render()` are both plain code with nothing to approximate.

`/review` against a clean repository — the empty-diff guard fires, proven by handing `Reviewer` a client that throws if it is ever called:

```text
Nothing to review — there are no unstaged changes.
```

`/plan add caching to CustomerService` — the exact rendering `/plan` prints, with no notice after it:

```text
1. [ ] Inspect CustomerService and its current dependencies
2. [ ] Add IMemoryCache to the DI container
3. [ ] Cache the read path in CustomerService
4. [ ] Add tests for the cached path
```

A `/review` against a repository with real changes needs a live `OPENAI_API_KEY`, since the report itself is the model's judgment, not this lesson's code. What reaches it is exactly `ReviewAsync` above: `git_diff`'s real output — captured for real against a temporary repository in Module 14's own lesson — becomes the one user message `Reviewer` sends, answered against the five concerns in its prompt.
