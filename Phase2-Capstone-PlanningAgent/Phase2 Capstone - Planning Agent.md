# Phase 2 Capstone — Planning Agent (v0.2)

## Project Overview

Phase 2 gave MiniCode a memory and a plan. This Capstone stamps that as **v0.2**, and fixes two seams that only show up when you look at the Phase as a whole — one of which is a piece of accounting that Module 9 silently broke.

## Prerequisites

**Starting point:** open `Module09-PlanningAndTaskState/`.

All of Phase 2 — repository conventions (Module 7), the context budget and search-before-read (8a), working state and compaction (8b), and plans with task state (9).

## Setup

Nothing new. No package changes; `MiniCode.Workspace` still has zero package references.

## Core Concepts

**A Phase is where the interactions surface.** Each Module in Phase 2 was verified on its own and each one worked. The two defects below only exist *between* Modules — one where Module 9 does not know about Module 7, and one where Module 9 quietly invalidated Module 8a. Neither is visible from inside either Module, which is the argument for looking at a Phase as a unit.

**Seam one: the planner could not see the repository's conventions.** Module 7 loads an `AGENTS.md` and folds it into the agent's instructions. Module 9 then built a planner that makes its *own* model call — with its own system prompt, and none of those conventions. So a repository whose rules say *add tests for new functionality* produced plans with a test step only when the model happened to think of one. The conventions now reach the planner, framed as a constraint on the plan rather than as advice.

**Seam two: planning was free, according to the budget.** Module 8a's whole point is that the context window is measurable. Module 9 then added a second model call per turn — and nothing counted it. `_usedTokens` tracked the request and the streamed answer, and the planning round trip simply did not exist. The budget was not slightly off; it was systematically wrong by one whole call every turn, and the number on screen looked healthy the entire time. The lesson generalises: **a measurement is only honest while every path that spends the thing reports it.**

**What v0.2 is.** Point MiniCode at a repository it has never seen and it will read that repository's own rules, tell you what it intends to do before it does anything, track how far it has got, revise the plan when the repository turns out to be different from what it assumed, and keep working after the conversation grows past the window — without touching a single file.

**What v0.2 is not.** It cannot write, run a command, use Git, or ask permission — it needs no permission for anything it can currently do. It also plans on *every* turn, including "what is this repository?", which is two model calls to answer a question that needed none. Module 16 makes planning a mode you ask for; until then it is unconditional, and the lesson says so rather than hiding it behind a demo that only asks planning-shaped questions.

## The Code

### `src/MiniCode.Cli/MiniCodeVersion.cs`

```csharp
namespace MiniCode.Cli;

/// <summary>
/// The version MiniCode reports at startup. It moves at a Phase Capstone and
/// nowhere else: 0.1 was the read-only Repository Explorer, 0.2 is the Planning
/// Agent that closes Phase 2. It lives in the CLI because versioning is a
/// packaging concern, and packaging is where Module 18 picks this constant up.
/// </summary>
internal static class MiniCodeVersion
{
    public const string Current = "0.2";
}
```

Seam one — the planner is handed the repository's conventions, from `src/MiniCode.Agent/Planner.cs`:

```csharp
    public async Task<TaskPlan> CreateAsync(string request, CancellationToken cancellationToken = default)
    {
        string system = string.IsNullOrWhiteSpace(conventions)
            ? Prompt
            : Prompt + Environment.NewLine + Environment.NewLine
              + "The repository states these conventions. A plan that ignores them is wrong:"
              + Environment.NewLine + conventions.Trim();

        ChatResponse response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.System, system), new ChatMessage(ChatRole.User, request)],
            new ChatOptions { Tools = null, ToolMode = ChatToolMode.None },
            cancellationToken);

        return Parse(request, response.Text);
    }
```

Seam two — the planning call is charged to the budget, from `src/MiniCode.Agent/CodingAgent.cs`:

```csharp
    public async Task<TaskPlan> PlanAsync(string request, CancellationToken cancellationToken = default)
    {
        TaskPlan plan = await _planner.CreateAsync(request, cancellationToken);

        // Planning is a model call like any other. Module 8a's budget is only
        // honest if every call it does not see is charged to it explicitly.
        _usedTokens += TokenEstimator.Estimate(request) + TokenEstimator.Estimate(plan.Render());
        return plan;
    }
```

`CodingAgentFactory` builds the planner with `new Planner(chatClient, instructions.Load())` — the same loaded `AGENTS.md` that already reaches the agent's instructions, now reaching the plan as well.

## Walkthrough

1. **`conventions` is an optional constructor parameter**, so a `Planner` built without one behaves exactly as Module 9's did. Every existing caller keeps compiling and the fix is additive.
2. **The conventions are framed as a constraint, not as advice** — *"a plan that ignores them is wrong"*. The same text in the agent's instructions is guidance for writing code; here it is a test the plan has to pass.
3. **Module 7's precedence still holds.** These are the repository's rules about *how its code should be written*, which is exactly the half the repository is allowed to control. Nothing about what MiniCode may do comes from this text.
4. **`PlanAsync` changed from an expression body to a method** so it can do two things: get the plan, then charge for it. That is the whole of seam two.
5. **The charge is an estimate of the request plus the rendered plan**, not of the full model exchange — the prompt and the model's prose are not counted. It under-reports, and under-reporting a little is very different from not counting at all.
6. **Nothing else in the solution changed.** Four files differ — the version, the planner, the agent, and the one line in the factory that wires them — and thirty-eight are carried forward byte-for-byte from Module 9.

## Exercise

**One:** plan only when planning is worth it. MiniCode currently makes two model calls for *"what does this repository do?"*, and the plan it produces for a question is noise. Add a cheap decision — before planning, judge whether the request is a task or a question, and skip the plan for questions. Acceptance criteria: a question produces one model call and no plan; a request phrased as work (*"add caching to CustomerService"*) still produces a plan; the decision itself must not cost a model call, because a call to decide whether to make a call is not a saving. Module 16 replaces this with an explicit mode, so favour something simple you would be happy to delete.

**Two:** carry the plan across a compaction. `WorkingState` preserves *what was asked*, but the plan itself is regenerated from scratch every turn and its task states are lost. Give `WorkingState` a fourth note kind for the current plan, render it into the state block, and have the agent keep the plan rather than discarding it. Acceptance criteria: after a compaction the state block shows the plan with its per-step markers intact; a completed step does not revert to pending; a session with no plan renders exactly as it does today.

Neither has a reference implementation in a later Module folder.

## Expected Output

The numbers below are real, measured against this Capstone's own folder.

The repository's own `AGENTS.md` is fourteen lines, and it now reaches both the agent and the planner:

```text
AGENTS.md loaded: 14 lines

1. [ ] Inspect CustomerService
2. [ ] Add IMemoryCache to the DI container
3. [ ] Modify the service
4. [ ] Add tests for the cached path
5. [ ] Build
6. [ ] Test
No files have been modified.

instructions      ~  417 tokens
planning call     ~   56 tokens  (uncounted before this Capstone)
context ~473 / 128,000 tokens (0%)
```

Step 4 is the one to point at on camera. The request said nothing about tests — the repository's conventions did, and before this Capstone the planner never saw them.

And a full session at v0.2:

```text
> dotnet run --project src/MiniCode.Cli -- D:\Repos\CustomerPortal

MiniCode v0.2
Workspace: D:\Repos\CustomerPortal
MiniCode. Type 'exit' to quit.

> Add caching to CustomerService.

1. [ ] Inspect CustomerService and its current dependencies
2. [ ] Check whether the solution already registers a cache
3. [ ] Add IMemoryCache to the DI container
4. [ ] Cache the read path in CustomerService
5. [ ] Add tests for the cached path
6. [ ] Build
7. [ ] Test
No files have been modified.

I've looked at src/CustomerPortal.Core/CustomerService.cs:24 — GetByIdAsync goes
straight to the repository on every call, and nothing in Program.cs registers a
cache. The plan above is what I'd do; I can't make any of these changes yet.

context ~2,190 / 128,000 tokens (2%)

> exit
```

That last sentence is v0.2 being honest. It can read the repository, read the repository's rules, decide what it would do, and say so — and it cannot do any of it. Phase 3 is where that changes, and Module 15 is where somebody finally gets asked first.
