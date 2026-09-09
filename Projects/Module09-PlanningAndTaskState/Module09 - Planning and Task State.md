# Module 9 — Planning and Task State

## Project Overview

Before MiniCode does anything to a repository, it says what it intends to do. We turn a request into a numbered plan the operator can read, track how far each step has got, and revise the plan when the repository turns out not to be what we assumed. Nothing is modified — and in Module 16 this becomes the `/plan` mode.

## Prerequisites

**Starting point:** open `Module08b-WorkingStateAndCompaction/`.

Module 8b's `WorkingState`, which already keeps *what was asked* across a compaction — a plan is the structured version of that, and Module 3's `ICodingAgent`, which grows one member here.

Last Module of Phase 2. Ships as part of **Phase 2 Capstone — Planning Agent (v0.2)**.

## Setup

Nothing new.

## Core Concepts

**A plan is a promise you can check before it is kept.** An agent that starts working and narrates as it goes gives you no moment to say *no, not like that*. A plan moves that moment to the front, where it is cheap.

**Planning is a separate call with no tools.** The planning request is made with `Tools = null` and `ToolMode = None`, so the model physically cannot invoke anything while it is planning — it can only think and answer. Today MiniCode has no write tools anyway, so this costs nothing; from Module 10 it is the difference between showing a plan and quietly executing one.

**Every plan is followed by one line: `No files have been modified.`** Not because it is technically necessary, but because the operator should never have to wonder. A guarantee nobody states is a guarantee nobody trusts.

**Four states, and `Blocked` is the interesting one.** Pending, in-progress and complete are bookkeeping. *Blocked* is the state that stops an agent inventing its way past a problem — it carries a note saying why, and the plan halts there rather than continuing to step four as though step three had worked.

**Task identity is not task position.** A step's `Id` is assigned once and never reused. That matters the moment a plan is revised: renumbering would make the operator's memory wrong, and would let a finished step silently become a different step. A revised plan legitimately reads `1, 2, 8, 9, 10` — the gap is the point.

**Revising keeps what is finished and replaces what is not.** MiniCode discovers things — there is no cache layer, the class is in a different project, the tests do not compile. When that happens the remaining steps are wrong and the completed ones are still true. `Revise` encodes exactly that, so replanning cannot rewrite history the operator already watched happen.

**The plan is immutable.** Every change returns a new plan. A plan that was printed cannot become a different plan afterwards, which is what makes "here is what I am going to do" mean anything.

**Parsing is forgiving about prose and strict about steps.** Models wrap lists in explanation. We take the numbered lines and ignore everything else, rather than demanding JSON and failing when a helpful sentence appears above it.

## The Code

### `src/MiniCode.Agent/TaskState.cs`

```csharp
namespace MiniCode.Agent;

/// <summary>Where a single step of a plan has got to.</summary>
public enum TaskState
{
    /// <summary>Not started.</summary>
    Pending,

    /// <summary>Being worked on now. At most one step should be here.</summary>
    InProgress,

    /// <summary>Finished.</summary>
    Complete,

    /// <summary>Cannot proceed, and says why. The plan stops rather than guessing.</summary>
    Blocked,
}
```

### `src/MiniCode.Agent/PlanTask.cs`

```csharp
namespace MiniCode.Agent;

/// <summary>
/// One step of a plan. <c>Id</c> is an identity, not a position: it is assigned
/// once and never reused, so a revised plan can drop or insert steps without
/// renumbering the ones already finished.
/// </summary>
/// <param name="Id">Stable identity for this step.</param>
/// <param name="Title">What the step does, in one line.</param>
/// <param name="State">How far it has got.</param>
/// <param name="Note">Why it is blocked, when it is.</param>
public sealed record PlanTask(int Id, string Title, TaskState State, string? Note = null)
{
    /// <summary>The marker shown against the step when the plan is rendered.</summary>
    public string Marker => State switch
    {
        TaskState.Complete => "x",
        TaskState.InProgress => ">",
        TaskState.Blocked => "!",
        _ => " ",
    };
}
```

### `src/MiniCode.Agent/TaskPlan.cs`

```csharp
namespace MiniCode.Agent;

/// <summary>
/// A request turned into steps the operator can read before anything happens.
/// Immutable: every change returns a new plan, so a plan that was shown cannot
/// quietly become a different one.
/// </summary>
/// <param name="Goal">The request this plan is for.</param>
/// <param name="Tasks">The steps, in the order they should be attempted.</param>
public sealed record TaskPlan(string Goal, IReadOnlyList<PlanTask> Tasks)
{
    /// <summary>No plan yet.</summary>
    public static TaskPlan None { get; } = new(string.Empty, []);

    /// <summary>True when there is nothing to show.</summary>
    public bool IsEmpty => Tasks.Count == 0;

    /// <summary>The first step not yet finished, or null when the plan is done.</summary>
    public PlanTask? Current => Tasks.FirstOrDefault(t => t.State is not TaskState.Complete);

    /// <summary>True when every step is complete.</summary>
    public bool IsComplete => !IsEmpty && Tasks.All(t => t.State == TaskState.Complete);

    /// <summary>Returns a plan with one step moved to a new state.</summary>
    public TaskPlan WithState(int id, TaskState state, string? note = null) =>
        this with
        {
            Tasks = [.. Tasks.Select(t => t.Id == id ? t with { State = state, Note = note } : t)],
        };

    /// <summary>
    /// Replaces the unfinished steps with new ones, keeping everything already
    /// complete and continuing the numbering — so revising a plan never rewrites
    /// history the operator has already watched happen.
    /// </summary>
    public TaskPlan Revise(IEnumerable<string> remainingTitles)
    {
        List<PlanTask> kept = [.. Tasks.Where(t => t.State == TaskState.Complete)];
        int next = Tasks.Count == 0 ? 1 : Tasks.Max(t => t.Id) + 1;

        return this with
        {
            Tasks = [.. kept, .. remainingTitles.Select(title => new PlanTask(next++, title, TaskState.Pending))],
        };
    }

    /// <summary>Renders the plan the way the operator sees it.</summary>
    public string Render() =>
        string.Join(Environment.NewLine, Tasks.Select(t =>
            $"{t.Id}. [{t.Marker}] {t.Title}{(t.Note is null ? "" : $"  ({t.Note})")}"));
}
```

The call that produces a plan, from `src/MiniCode.Agent/Planner.cs`:

```csharp
    public async Task<TaskPlan> CreateAsync(string request, CancellationToken cancellationToken = default)
    {
        ChatResponse response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.System, Prompt), new ChatMessage(ChatRole.User, request)],
            new ChatOptions { Tools = null, ToolMode = ChatToolMode.None },
            cancellationToken);

        return Parse(request, response.Text);
    }
```

`Parse` keeps lines matching `^\d+[.)]\s+(?<title>.+)$` and discards the rest. `ConsoleChatLoop` calls `PlanAsync` before each turn, prints the plan and the notice, then answers as before.

## Walkthrough

1. **`TaskPlan.None` rather than null.** A missing plan and an empty plan are the same thing to every caller, and one of them cannot throw.
2. **`Current` returns the first step that is not complete** — including a blocked one, deliberately. The blocked step *is* where the plan currently stands, and hiding it would make the plan look like it was progressing.
3. **`WithState` rebuilds the list.** Cheap at seven steps, and it means no caller can hold a step and mutate it behind the plan's back.
4. **`Revise` computes the next id from `Max(t => t.Id) + 1`**, not from the count. That is what produces `1, 2, 8, 9` and what stops a new step inheriting a finished step's number.
5. **The planning call passes `Tools = null` *and* `ToolMode = None`.** Belt and braces: one says there is nothing to call, the other says do not try.
6. **`NoChangesNotice` is a constant on `Planner`**, so the promise and the code that keeps it live in the same file.
7. **The prompt caps the plan at seven steps** and tells the model to ask a question rather than guess when the request is too vague. A plan of twenty steps is not a plan; it is a transcript.

## Exercise

**One:** make read-only structural rather than incidental. `CreateAsync` passes `Tools = null` at the call site — which works, and which the next person to add a parameter can silently undo. Write a `ReadOnlyChatClient` in `src/MiniCode.Agent/` that wraps an `IChatClient`, clones whatever `ChatOptions` it is handed, forces `Tools = null` and `ToolMode = ChatToolMode.None`, and have `Planner` wrap its client in one. Acceptance criteria: a caller who passes options *containing* tools still gets a tool-free request; planning behaves identically otherwise. Test it by handing it an options object with a tool named `write_file` — a tool that does not exist yet, and will in Module 10.

**Two:** let the operator drive the plan. Add commands to move a step — mark `3` in progress, mark `3` done, block `3` with a reason — and print the plan after each. Acceptance criteria: marking a step complete moves `Current` to the next incomplete step; blocking a step keeps it as `Current` and shows the reason; the plan is never mutated in place. Module 16 turns this into real CLI commands; the point here is that `TaskPlan`'s API is already enough to do it.

Neither has a reference implementation in a later Module folder.

## Expected Output

A plan parsed out of a model reply that also contained prose, then tracked, then revised. Real output from this Module's code:

```text
1. [ ] Inspect CustomerService
2. [ ] Identify existing caching infrastructure
3. [ ] Determine an appropriate cache lifetime
4. [ ] Modify the service
5. [ ] Add tests
6. [ ] Build
7. [ ] Test
No files have been modified.
```

Two steps done, one under way:

```text
1. [x] Inspect CustomerService
2. [x] Identify existing caching infrastructure
3. [>] Determine an appropriate cache lifetime
4. [ ] Modify the service
5. [ ] Add tests
6. [ ] Build
7. [ ] Test
current: Determine an appropriate cache lifetime   complete: False
```

And then MiniCode discovers there is no caching layer at all, so the remaining steps are wrong:

```text
-- replanned after discovering there is no cache layer --
1. [x] Inspect CustomerService
2. [x] Identify existing caching infrastructure
8. [ ] Add IMemoryCache to the DI container
9. [ ] Modify the service
10. [ ] Add tests
11. [ ] Build
12. [ ] Test
```

The jump from `2` to `8` is the whole idea. Steps 3 through 7 were never done and are gone; steps 1 and 2 were done and keep the numbers the operator already saw. Nothing that happened has been renumbered to make the list look tidy.
