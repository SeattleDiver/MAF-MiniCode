# Module 8a — Context Management: Budget and Search Before Read

## Project Overview

The context window is the scarcest thing MiniCode has, and until now it has been spending it blind. We give it a way to measure what a session costs, and a way to turn a search result into a short list of files worth opening — so it reads five files instead of forty-two.

## Prerequisites

**Starting point:** open `Module07-ProjectInstructionsWithAgentsMd/`.

Module 5b's `search_files` and `read_file`, and Module 7's `AgentInstructions.Compose` — the composed instruction block is the first thing that occupies the budget, on every single request.

Ships as part of **Phase 2 Capstone — Planning Agent (v0.2)**.

## Setup

Nothing new. Deliberately no tokenizer package: the estimate is arithmetic, and the lesson is honest about what that costs.

## Core Concepts

**The context window is a fixed budget, and everything competes for it.** The instructions, every question, every answer, and — by far the largest — every tool result. Read forty files into it and there is less room left for the model to actually think.

**Estimating is enough; measuring exactly is not worth it.** A real tokenizer is a dependency, a model-specific vocabulary, and a startup cost, to answer a question MiniCode only uses for *decisions*: is this session getting full, is this read worth it. Characters ÷ 4, plus a few tokens of framing per message, is close enough.

**But say that it is an estimate.** Every number MiniCode prints carries a `~`. An approximation presented as a fact is a small lie that someone eventually debugs at length. The ratio is genuinely good for prose and ordinary C#; it drifts on dense JSON and on heavily commented source, and that is fine for the decisions being made with it.

**Search before read is the whole discipline.** The naive loop is *list everything, read what looks relevant*. The disciplined one is *search for the thing, see which files it lives in, read those*. The first spends the budget to find out where to look; the second spends it on what it came for.

**Ranking by match count is a crude heuristic that works.** A file containing eight matches for `ResolvePath` is a better bet than one containing a single mention in a comment. It is not clever, and it does not need to be — it just needs to be better than alphabetical.

**A plan is capped, and admits it was capped.** Five files, most-matched first. If the search hit forty-two, the plan says so, so the model knows there is more to ask for rather than assuming it has seen everything. This is the same principle as the truncation notices in Module 5b, applied one level up.

**A threshold now, machinery later.** `ShouldCompact` returns true at 75% of the window. Nothing acts on it yet — Module 8b is what does something when it fires. Defining the line before building the response keeps that Module from having to invent a policy while it is also inventing a mechanism.

## The Code

### `src/MiniCode.Agent/TokenEstimator.cs`

```csharp
namespace MiniCode.Agent;

/// <summary>
/// An approximation of how many tokens a piece of text costs. It is deliberately
/// arithmetic rather than a real tokenizer: MiniCode needs a number good enough
/// to make a decision with, not one good enough to bill on.
/// </summary>
public static class TokenEstimator
{
    /// <summary>Roughly the characters-per-token ratio of English and C# together.</summary>
    public const int CharactersPerToken = 4;

    /// <summary>What each message costs in framing, on top of its text.</summary>
    public const int MessageOverhead = 4;

    /// <summary>Estimates one message. Never negative, never exact.</summary>
    public static int Estimate(string? text) =>
        string.IsNullOrEmpty(text) ? MessageOverhead : (text.Length / CharactersPerToken) + MessageOverhead;
}
```

### `src/MiniCode.Agent/ContextBudget.cs`

```csharp
namespace MiniCode.Agent;

/// <summary>
/// How much room the model has, and when MiniCode should start worrying. The
/// window is finite and shared by the instructions, the conversation and every
/// tool result — spending it on a file nobody needed is the cost this exists
/// to make visible.
/// </summary>
/// <param name="MaxTokens">The model's context window.</param>
/// <param name="CompactAtFraction">The fraction at which compaction becomes due.</param>
public sealed record ContextBudget(int MaxTokens, double CompactAtFraction)
{
    /// <summary>The window MiniCode assumes for gpt-4.1-mini.</summary>
    public static ContextBudget Default { get; } = new(128_000, 0.75);

    /// <summary>True once the session is close enough to full to need attention.</summary>
    public bool ShouldCompact(int usedTokens) => usedTokens >= MaxTokens * CompactAtFraction;

    /// <summary>A one-line report, with a tilde because the number is an estimate.</summary>
    public string Describe(int usedTokens) =>
        $"context ~{usedTokens:N0} / {MaxTokens:N0} tokens ({(double)usedTokens / MaxTokens:P0})";
}
```

### `src/MiniCode.Agent/ReadPlan.cs`

```csharp
namespace MiniCode.Agent;

/// <summary>
/// What to read after a search, and what it cost to find out. <c>Files</c> is
/// ranked most-matched first and capped, so a search that hit forty files still
/// produces a short list.
/// </summary>
/// <param name="Files">The files worth opening, best first.</param>
/// <param name="CandidateCount">How many distinct files matched at all.</param>
/// <param name="MatchCount">How many individual lines matched.</param>
public sealed record ReadPlan(IReadOnlyList<string> Files, int CandidateCount, int MatchCount)
{
    /// <summary>True when the search found more files than the plan will open.</summary>
    public bool IsNarrowed => CandidateCount > Files.Count;
}
```

### `src/MiniCode.Agent/RetrievalPlanner.cs`

```csharp
namespace MiniCode.Agent;

/// <summary>
/// Turns search hits into a short list of files worth opening. The ranking is
/// crude on purpose — a file that matched eight times is a better guess than one
/// that matched once, and that is the whole heuristic.
/// </summary>
public static class RetrievalPlanner
{
    /// <summary>The most files one plan will propose reading.</summary>
    public const int MaxFiles = 5;

    /// <summary>
    /// Builds a plan from <c>path:line: text</c> hits as returned by search.
    /// Lines that do not carry a path are ignored rather than guessed at.
    /// </summary>
    public static ReadPlan FromSearch(IReadOnlyList<string>? hits)
    {
        if (hits is null || hits.Count == 0)
        {
            return new ReadPlan([], 0, 0);
        }

        List<IGrouping<string, string>> byFile = [.. hits
            .Select(PathOf)
            .Where(p => p.Length > 0)
            .GroupBy(p => p)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)];

        return new ReadPlan(
            [.. byFile.Take(MaxFiles).Select(g => g.Key)],
            byFile.Count,
            byFile.Sum(g => g.Count()));
    }

    private static string PathOf(string hit)
    {
        int colon = hit.IndexOf(':', StringComparison.Ordinal);
        return colon > 0 ? hit[..colon] : string.Empty;
    }
}
```

The agent seeds the tally with the instruction block in its constructor, then charges each turn as it happens. The contiguous heart of it, from `src/MiniCode.Agent/CodingAgent.cs`:

```csharp
        _usedTokens += TokenEstimator.Estimate(request);

        await foreach (AgentResponseUpdate update in
            _agent.RunStreamingAsync(request, _session, cancellationToken: cancellationToken))
        {
            string fragment = update.ToString();
            _usedTokens += TokenEstimator.Estimate(fragment);
            yield return fragment;
        }
```

`ContextSummary` on `ICodingAgent` returns `_budget.Describe(_usedTokens)`, and `ConsoleChatLoop` prints it after each answer — one added line in each file.

## Walkthrough

1. **`Estimate` charges `MessageOverhead` even for empty text**, because an empty message still costs role framing. Small, but it stops a long conversation of short turns from reading as free.
2. **`ContextBudget` is a record with two numbers and no state.** The used count lives on whoever is counting; the budget only knows the window and the threshold.
3. **`Describe` prints a tilde.** That character is the difference between a helpful estimate and a wrong number.
4. **`ShouldCompact` exists with nothing calling it.** That is deliberate and worth saying on camera: the policy is decided here, the mechanism arrives in 8b.
5. **`FromSearch` never reads a file.** It works entirely on the `path:line: text` strings search already returned — which is the point. Planning what to read must not cost what reading costs.
6. **`ThenBy` on the path** makes ties deterministic. Without it, two files with equal match counts could swap order between runs and the plan would look unstable for no reason.
7. **`PathOf` returns empty rather than guessing** when a line has no colon, and those lines are filtered out. A malformed hit should shrink the plan, not corrupt it.
8. **The agent counts what it sends and what it receives.** Not what the provider billed — that number arrives with the response and is Module 17's business. This one is available *before* a decision has to be made.

## Exercise

**One:** count the tool results too. The tally currently misses the largest thing in the window — a `read_file` result of four hundred numbered lines is far bigger than the question that asked for it, and it never reaches `RunStreamingAsync`'s output. Route tool results through the tally. The natural place is Module 5a's `IToolInvoker` seam: a `CountingToolInvoker` that estimates each result on the way back and reports the total. Acceptance criteria: reading a 400-line file moves the context line by roughly the size of that file; the count is unchanged when no tool is called; no tool class is edited.

**Two:** make the planner budget-aware. `FromSearch` caps at five files regardless of how large they are — five 2,000-line files is a worse plan than eight small ones. Give it a token allowance and have it take files until the allowance is spent. You will need a size per candidate; getting one without reading the file is the interesting part of the problem, and `IFileSystemService` does not currently offer it. Acceptance criteria: a plan never proposes more than its allowance; a single file larger than the whole allowance is still proposed, alone, rather than producing an empty plan.

Both belong in `src/MiniCode.Agent/`. Neither has a reference implementation in a later Module folder.

## Expected Output

The numbers below are real, measured against this Module's own folder.

Searching for `ResolvePath` and planning from the result:

```text
search 'ResolvePath': 7 matches in 5 files
plan: read 5
  src/MiniCode.Workspace/FileSystemService.cs
  src/MiniCode.Workspace/InstructionSource.cs
  src/MiniCode.Workspace/IWorkspace.cs
  src/MiniCode.Workspace/RepositoryInspector.cs
  src/MiniCode.Workspace/Workspace.cs
```

And the argument for doing it that way, priced:

```text
read every file : ~12,114 tokens  (9% of window)
read the plan   : ~2,909 tokens  (2% of window)
```

Four times cheaper, on a solution of twenty-eight files. On a real repository of two thousand files the ratio is not four — it is the difference between fitting and not fitting.

In conversation, the cost of the session is now visible after every turn:

```text
> dotnet run --project src/MiniCode.Cli -- D:\MAF-SmartAgent\Module08a-BudgetAndSearchBeforeRead

MiniCode v0.1
Workspace: D:\MAF-SmartAgent\Module08a-BudgetAndSearchBeforeRead
MiniCode. Type 'exit' to quit.

> Where is a path checked before a file is opened?

Workspace.ValidatePath in src/MiniCode.Workspace/Workspace.cs:27 is the single
check. ResolvePath and IsPathAllowed both delegate to it, and FileSystemService
calls ResolvePath before every read — src/MiniCode.Workspace/FileSystemService.cs:31.

context ~1,043 / 128,000 tokens (1%)

> exit
```

Note where the session starts from: the composed instruction block alone is about 137 tokens before a word is exchanged, and it is re-sent on every request. That is the floor the budget is measured against.
