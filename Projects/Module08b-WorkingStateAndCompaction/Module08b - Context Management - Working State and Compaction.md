# Module 8b — Context Management: Working State and Compaction

## Project Overview

Module 8a decided *when* a session is too full. This Module decides what to do about it: rewrite the conversation, replacing everything old with a short statement of what MiniCode learned from it. The interesting question is not how to delete messages — it is which facts must survive.

## Prerequisites

**Starting point:** open `Module08a-BudgetAndSearchBeforeRead/`.

Module 8a's `TokenEstimator` and `ContextBudget.ShouldCompact`, and Module 3's decision that `CodingAgent` owns the `AgentSession` — which is the only reason the history can be rewritten from here at all.

Ships as part of **Phase 2 Capstone — Planning Agent (v0.2)**.

## Setup

Nothing new.

> **Currency note.** `AgentSession` does **not** expose its messages as a property — it has `StateBag` and `GetService`, and nothing else. History is reached through the extension methods `TryGetInMemoryChatHistory(out List<ChatMessage>)` and `SetInMemoryChatHistory(...)` in `Microsoft.Agents.AI`. Note also that MAF 1.20.0 ships a whole `Microsoft.Agents.AI.Compaction` namespace with ready-made strategies; MiniCode does not use it, because its index type requires a `Microsoft.ML.Tokenizers` dependency and because configuring a strategy would teach configuration rather than the decision this Module is about.

## Core Concepts

**A long session dies quietly.** It does not error — it fills, and the model starts losing the beginning. Compaction is what you do before that, on purpose, so that what is lost is chosen rather than whatever fell off the front.

**The question is not "what can I delete" but "what must survive".** Invert it and the design follows. Three things must survive a compaction, and everything else in the conversation is expendable:

- **What was asked.** A request the operator made and MiniCode has not finished is the single worst thing to forget. It will confidently do something else.
- **What was established.** The conclusions reached, including any decision MiniCode explained — the reasoning is gone, but the answer stays.
- **Which files have been examined.** Otherwise the agent re-reads what it already read, which is the exact cost compaction exists to reclaim.

**Tool output is the bulkiest thing in the window and the most disposable.** A four-hundred-line `read_file` result dwarfs the question that asked for it. It is also the thing least worth keeping — the *fact that the file was read* survives as a note, the four hundred lines do not. Getting this wrong is easy: a first draft of this Module summarised tool results into "what was established," which turned eight thousand characters of file content into a nonsense fact.

**Compaction here is deterministic — no model call.** A summarising request costs a round trip, can fail, and can itself be too large to send at exactly the moment you have run out of room. Scanning the messages and keeping four kinds of thing is arithmetic, and arithmetic cannot fail when the window is full.

**Recent turns are kept verbatim.** The model is usually mid-thought. A summary of the last exchange is strictly worse than the exchange, so the newest messages are never touched.

**And the cut must land on a user message.** Slice the history at an arbitrary index and you can separate a tool call from the result that answers it, leaving the model with a question it asked and no reply. Walking backwards to the nearest user message costs four lines and removes the whole class of bug.

## The Code

### `src/MiniCode.Agent/WorkingNoteKind.cs`

```csharp
namespace MiniCode.Agent;

/// <summary>What a working note is about. These are the things that must survive
/// compaction — everything else in the conversation is expendable.</summary>
public enum WorkingNoteKind
{
    /// <summary>Something the operator asked for. Kept until it is clearly done.</summary>
    OpenTask,

    /// <summary>A conclusion MiniCode reached, including any decision it explained.</summary>
    CompletedWork,

    /// <summary>A file MiniCode has already looked at.</summary>
    FileState,
}
```

### `src/MiniCode.Agent/WorkingNote.cs`

```csharp
namespace MiniCode.Agent;

/// <summary>One fact worth carrying across a compaction boundary.</summary>
/// <param name="Kind">Which sort of fact it is.</param>
/// <param name="Text">The fact itself, already short enough to keep.</param>
public sealed record WorkingNote(WorkingNoteKind Kind, string Text);
```

Distilling a run of messages, from `src/MiniCode.Agent/WorkingState.cs`:

```csharp
    public static WorkingState From(IEnumerable<ChatMessage> messages)
    {
        var notes = new List<WorkingNote>();

        foreach (ChatMessage message in messages)
        {
            foreach (FunctionCallContent call in message.Contents.OfType<FunctionCallContent>())
            {
                Add(notes, WorkingNoteKind.FileState, PathArgument(call));
            }

            if (message.Role == ChatRole.Tool)
            {
                // Tool output is the bulkiest thing in the window and the most
                // disposable: the file it came from is already noted above.
                continue;
            }

            Add(notes,
                message.Role == ChatRole.User ? WorkingNoteKind.OpenTask : WorkingNoteKind.CompletedWork,
                message.Text);
        }

        return new WorkingState(notes);
    }
```

And rendering it as the message that replaces them:

```csharp
    public string Render()
    {
        var sections = new List<string>();

        foreach (WorkingNoteKind kind in Enum.GetValues<WorkingNoteKind>())
        {
            List<WorkingNote> section = [.. Notes.Where(n => n.Kind == kind)];
            if (section.Count > 0)
            {
                sections.Add(Label(kind) + Environment.NewLine
                    + string.Join(Environment.NewLine, section.Select(n => "- " + n.Text)));
            }
        }

        string blank = Environment.NewLine + Environment.NewLine;
        return "SESSION STATE (earlier turns were compacted away)" + blank + string.Join(blank, sections);
    }
```

### `src/MiniCode.Agent/SessionCompactor.cs`

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace MiniCode.Agent;

/// <summary>
/// Replaces the older part of a session's history with one message describing
/// what it contained. Recent turns are kept verbatim: the model is usually
/// mid-thought, and a summary of the last exchange is worse than the exchange.
/// </summary>
public static class SessionCompactor
{
    /// <summary>How many recent messages are always kept exactly as they are.</summary>
    public const int PreservedMessages = 6;

    /// <summary>
    /// Compacts the session in place. Returns the estimated tokens reclaimed, or
    /// zero when there was nothing worth doing.
    /// </summary>
    public static int Compact(AgentSession session)
    {
        if (!session.TryGetInMemoryChatHistory(out List<ChatMessage>? history) || history is null)
        {
            return 0;
        }

        int cut = CutPoint(history);
        if (cut <= 0)
        {
            return 0;
        }

        WorkingState state = WorkingState.From(history.Take(cut));
        if (state.IsEmpty)
        {
            return 0;
        }

        int before = Estimate(history);
        List<ChatMessage> compacted = [new ChatMessage(ChatRole.User, state.Render()), .. history.Skip(cut)];
        session.SetInMemoryChatHistory(compacted);
        return before - Estimate(compacted);
    }

    // The cut lands on a user message, so a tool call is never separated from
    // the tool result that answers it.
    private static int CutPoint(IReadOnlyList<ChatMessage> history)
    {
        for (int i = history.Count - PreservedMessages; i > 0; i--)
        {
            if (history[i].Role == ChatRole.User)
            {
                return i;
            }
        }

        return 0;
    }

    private static int Estimate(IEnumerable<ChatMessage> messages) =>
        messages.Sum(m => TokenEstimator.Estimate(m.Text));
}
```

`CodingAgent` closes the loop in four lines: after a turn finishes streaming, if `_budget.ShouldCompact(_usedTokens)` it calls `SessionCompactor.Compact(_session)` and subtracts what came back from its running tally.

## Walkthrough

1. **`From` reads file paths off `FunctionCallContent` before it decides anything else**, so a tool call contributes a `FileState` note even though its result is about to be discarded.
2. **`ChatRole.Tool` messages are skipped outright.** This is the line that fixes the mistake described above — without it, file contents get summarised as conclusions.
3. **`Add` de-duplicates and truncates.** Ask the same question twice and it appears once; a rambling answer is clipped to 160 characters. The state block must stay small, or compaction just moves the problem.
4. **`Render` groups by kind in enum order**, so the block always reads *asked → established → examined*. Stable output is easier to eyeball on camera and easier to diff.
5. **`Compact` returns zero three separate times** — no history, nothing old enough, nothing worth keeping. A compaction that would achieve nothing does not happen, and the caller can tell.
6. **The replacement message is authored as `ChatRole.User`.** It is context handed to the model, not something the model said. Attributing it to the assistant would invite it to treat its own summary as a prior claim.
7. **`Estimate` counts `m.Text` only**, so it under-reports messages whose payload is a tool call rather than prose. It is an estimate used to report savings, not to gate anything, and the number is printed with a tilde.

## Exercise

**One:** keep a note that a task is still open. `OpenTask` currently collects every question the operator ever asked, so a long session accumulates a list of things that are mostly finished. Give `WorkingState` a way to retire a request once MiniCode has clearly answered it — and be careful about the failure mode: forgetting an unfinished task is far worse than carrying a finished one, so when in doubt, keep it. Acceptance criteria: a request followed by an answer that addresses it stops appearing after the next compaction; a request the agent never got to still appears; nothing is retired on the strength of the model merely having replied.

**Two:** compact tool results instead of discarding them. Rather than dropping a `read_file` result entirely, replace it with one line — `read src/Foo.cs (412 lines)` — so the model knows the shape of what it saw. This is `SessionCompactor`'s territory, and the interesting part is that it changes what "expendable" means: the result becomes small rather than absent. Acceptance criteria: a compacted history contains one line per tool call rather than the full output; the reclaimed-token count still reflects a real saving; the cut still lands on a user message.

Neither has a reference implementation in a later Module folder.

## Expected Output

A ten-message session, compacted. These numbers are real, produced by this Module's code against a scripted history containing two file reads of 8,000 and 6,000 characters:

```text
history: 10 messages, ~3,620 tokens

SESSION STATE (earlier turns were compacted away)

What was asked:
- Where is a path checked before a file is opened?

What was established:
- ValidatePath in Workspace.cs is the single check; ResolvePath and IsPathAllowed both delegate to it.

Files already examined:
- src/MiniCode.Workspace/Workspace.cs

after compaction: ~1,649 tokens  (reclaimed ~1,971)
```

Fifty-four percent of the session reclaimed, and read what survived: the question, the answer, and the fact that one file has already been opened. The eight thousand characters of file content are gone, and nothing that mattered went with them.

In a real session the effect is invisible until it fires, which is the point — the context line from Module 8a simply stops climbing:

```text
> Now check whether anything else calls it.

FileTools is the only other caller, in src/MiniCode.Tools/FileTools.cs — it
delegates to the service rather than resolving paths itself.

context ~1,701 / 128,000 tokens (1%)
```

Set `ContextBudget.Default` to a window of a few thousand tokens and you can watch that number rise, drop, and carry on — with MiniCode still able to answer a follow-up about the file it read before the boundary.
