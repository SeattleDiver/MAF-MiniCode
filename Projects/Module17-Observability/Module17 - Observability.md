# Module 17 — Observability

## Project Overview

Every prior Module made MiniCode more capable; none made it legible from the outside. This Module adds structured tracing — `[Agent]`, `[Tool]`, `[Result]` lines naming every request, tool call, its duration, and its outcome — written to stderr so it never disturbs the chat transcript, and turns on MAF's own built-in agent logging along the way.

## Prerequisites

**Starting point:** open `Module16b-PlanAndReviewModes/`.

Module 5a's `IToolInvoker` seam — its own doc comment has named this Module as the reason it exists, the same way it named Module 15 for approval. Module 15's `ApprovalInvoker` and Module 12's `LoopGuardInvoker`, which this now sits between.

Fifth Module of Phase 4. Ships as part of **Phase 4 Capstone — Developer CLI (v0.4)**.

## Setup

`Microsoft.Extensions.Logging` in `MiniCode.Agent`, `Microsoft.Extensions.Logging.Abstractions` in `MiniCode.Infrastructure` — both pinned at `10.0.11`, the exact version already resolving transitively through `Microsoft.Extensions.AI.OpenAI`. No other package changes.

## Core Concepts

**`ChatClientAgent` already knows how to log itself — it was never asked to.** The same simple constructor MiniCode has called since Module 3 has always accepted a trailing `ILoggerFactory` parameter. Pass one in, and MAF's own internals start reporting "Invoking client" and "Invoked client" around every request, at `Debug` and `Information`, with no code of MiniCode's own involved. This is the Module's "MAF telemetry" — verified by wiring a real `ILoggerFactory` into a real `ChatClientAgent` and reading what came out, not assumed from a changelog.

**MAF's own logging is coarse; the syllabus's example is not.** "Invoked client" brackets a whole streaming call — it says nothing about which tool ran or how long it took. The `[Tool]`/`[Result]` granularity the example wants is MiniCode's own, built the same way Module 15 built approval: another `IToolInvoker` decorator.

**`TracingInvoker` sits inside `ApprovalInvoker`, not outside it.** Duration is meant to answer "how long did the tool take," not "how long did a human take to decide." Wrapping `DirectToolInvoker` directly means a pending Y/N/A prompt is never counted as part of a tool's own time.

**A trace line does not require a success/failure classifier.** Every tool in this solution already says what happened in its own returned text — `"Exit code 0"`, `"...failed: ..."`, `"...is not on the allow list."` `TracingInvoker` logs the first line of that text verbatim. A failed operation is visible because its own words say so, not because something inspected the string and decided.

**A custom `ILoggerProvider` is the whole of "structured logging" here.** `BracketLoggerProvider` renders a log entry's category in brackets, then its message — nothing about severity, timestamps, or scopes, because the syllabus's own example shows none. `loggerFactory.CreateLogger("Tool")` and `loggerFactory.CreateLogger("Result")` are what turn into `[Tool]` and `[Result]`; the category *is* the label.

**Trace output goes to stderr, chat output stays on stdout.** `dotnet run --project src/MiniCode.Cli -- <path> 2> trace.log` is how an operator captures a complete run's trace without a single line of it appearing in the transcript they were already reading.

**`CodingAgentFactory` builds the logger factory, not `MiniCode.Cli`.** `BracketLoggerProvider` lives in `MiniCode.Infrastructure`, which `Cli` cannot reference — the composition root already builds everything else the agent needs, and `Program.cs` does not change at all this Module.

## The Code

### `src/MiniCode.Infrastructure/BracketLoggerProvider.cs`

```csharp
using Microsoft.Extensions.Logging;

namespace MiniCode.Infrastructure;

/// <summary>
/// Renders every log entry as its category in brackets, then the message —
/// <c>[Tool] read_file</c>, <c>[Result] ...</c> — and nothing else. Written to
/// <see cref="Console.Error"/>, so redirecting stderr is how an operator
/// captures a trace without disturbing the chat transcript on stdout.
/// </summary>
public sealed class BracketLoggerProvider : ILoggerProvider
{
    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new BracketLogger(categoryName);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class BracketLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Console.Error.WriteLine($"[{category}] {formatter(state, exception)}");
    }
}
```

### `src/MiniCode.Agent/TracingInvoker.cs`

```csharp
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MiniCode.Tools;

namespace MiniCode.Agent;

/// <summary>
/// Wraps Module 5a's <see cref="IToolInvoker"/> seam and logs every call that
/// actually reaches a tool — its name, its outcome, and how long it took.
/// Sits inside <see cref="ApprovalInvoker"/>, so a human's time spent deciding
/// is never counted as the tool's own duration.
/// </summary>
public sealed class TracingInvoker(IToolInvoker inner, ILoggerFactory loggerFactory) : IToolInvoker
{
    private readonly ILogger _toolLog = loggerFactory.CreateLogger("Tool");
    private readonly ILogger _resultLog = loggerFactory.CreateLogger("Result");

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(ToolInvocation invocation, CancellationToken cancellationToken)
    {
        _toolLog.LogInformation("{ToolName}", invocation.Name);

        var stopwatch = Stopwatch.StartNew();
        object? result = await inner.InvokeAsync(invocation, cancellationToken);
        stopwatch.Stop();

        _resultLog.LogInformation("{Summary} ({ElapsedMilliseconds}ms)", FirstLine(result), stopwatch.ElapsedMilliseconds);
        return result;
    }

    // A tool's own text already says what happened — "Exit code 0", "Wrote
    // src/A.cs (12 lines)", "run_command failed: ..." — the first line is
    // enough for a trace without repeating a whole build's output twice.
    private static string FirstLine(object? result) =>
        (result?.ToString() ?? string.Empty).Split(Environment.NewLine, 2)[0];
}
```

Turning it on and wiring it into the chain, from `src/MiniCode.Agent/CodingAgentFactory.cs`:

```csharp
        // The composition root builds this, not MiniCode.Cli — the logging
        // provider lives in MiniCode.Infrastructure, which Cli cannot reference.
        // CodingAgent owns disposing it, alongside the chat client.
        ILoggerFactory loggerFactory = LoggerFactory.Create(
            builder => builder.AddProvider(new MiniCode.Infrastructure.BracketLoggerProvider()));
```

```csharp
        var tracing = new TracingInvoker(new DirectToolInvoker(), loggerFactory);
        var approval = new ApprovalInvoker(tracing, prompter);
```

`ChatClientAgent`'s existing call site gains one named argument: `loggerFactory: loggerFactory` — MAF's own logging turns on the moment one is supplied.

What `RunStreamingAsync` logs for the model's own words, from `src/MiniCode.Agent/CodingAgent.cs`:

```csharp
    public async IAsyncEnumerable<string> RunStreamingAsync(
        string request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _guard.Reset();
        _usedTokens += TokenEstimator.Estimate(request);
        _agentLog.LogInformation("Task started");

        await foreach (AgentResponseUpdate update in
            _agent.RunStreamingAsync(request, _session, cancellationToken: cancellationToken))
        {
            string fragment = update.ToString();
            _usedTokens += TokenEstimator.Estimate(fragment) + TokenEstimator.Estimate(ToolActivityText(update));

            if (fragment.Length > 0)
            {
                // TracingInvoker logs every tool call already, so only the model's
                // own words are traced here — logging the tool announcement below
                // too would report the same call under two different categories.
                _agentLog.LogInformation("{Text}", fragment.Trim());
                yield return fragment;
                continue;
            }
```

`CodingAgent`'s constructor gains an `ILoggerFactory loggerFactory` parameter, stores it for `Dispose()` to close alongside `_chatClient`, and builds `_agentLog` from it exactly as `TracingInvoker` builds its own two loggers.

## Walkthrough

1. **`TracingInvoker` never inspects a result's meaning, only its shape.** `FirstLine` takes the text up to the first newline whatever that text says — the same discipline `ShellTools` already applies when it caps output, applied here to what gets logged instead of what gets shown.
2. **Two log calls per tool invocation, always in the same order.** `[Tool]` before `inner.InvokeAsync`, `[Result]` after — a trace reader never sees a result without the call that produced it, because nothing between them can throw past `DirectToolInvoker`'s own never-throw guarantee.
3. **The category string is the entire formatting decision.** `BracketLoggerProvider` does not know "Tool" or "Result" or "Agent" are special — it brackets whatever category it is given, which is what lets MAF's own `Microsoft.Agents.AI.ChatClientAgent` category flow through the identical sink under its own name, unrelabeled.
4. **A denied or refused call still gets traced,** because `TracingInvoker` wraps `DirectToolInvoker` directly and never asks whether the tool "succeeded" — only `ApprovalInvoker`, sitting outside it, can stop a call before `TracingInvoker` ever sees it, and a call it stops was never attempted in the first place.
5. **`Program.cs` has nothing to change.** The logger factory is built and owned entirely inside `CodingAgentFactory`, so this Module's whole effect is invisible to `MiniCode.Cli`'s own two files.

## Exercise

`TracingInvoker` reports every tool at the same `LogInformation` level, so a trace reader cannot filter for trouble without reading every line. Change `InvokeAsync`, in `src/MiniCode.Agent/TracingInvoker.cs`, to log the result at `LogWarning` when its first line contains the word `"failed"` or starts with `"Exit code"` followed by anything other than `0`, and at `LogInformation` otherwise. Acceptance criteria: a successful `run_command` or file write still logs at `Information`; a nonzero exit code or a `"failed:"` message logs at `Warning`; `BracketLoggerProvider` needs no change, since `ILogger.Log`'s `logLevel` parameter already reaches it — only nothing in this Module's formatter currently uses it. No reference implementation ships in a later Module folder.

## Expected Output

All of the following is real, captured output — a real `TracingInvoker` over a real temporary Git repository, and a real `ChatClientAgent` wired to a scripted `IChatClient`, both logging through the same production `BracketLoggerProvider`.

`[Tool]`/`[Result]` for a successful call and a refused one — the second is `run_command("git", ["status", "--porcelain"])`, which is not on Module 11b's allow list (`git_status` is the tool for that; `run_command` only allows `git commit`), captured exactly as `TracingInvoker` saw it:

```text
[Tool] list_files
[Result] Calculator.cs (10ms)
[Tool] run_command
[Result] run_command failed: 'git status --porcelain' is not on the allow list. Allowed: dotnet restore, dotnet build, dotnet test, dotnet format, git commit. (3ms)
```

MAF's own built-in logging, from a real `ChatClientAgent` given the same `ILoggerFactory`:

```text
[Microsoft.Agents.AI.ChatClientAgent] [RunStreamingAsync] Agent 3f3c9bec6ad54e23858892773a8b536c/MiniCode Invoking client ScriptedChatClient.
[Microsoft.Agents.AI.ChatClientAgent] [RunStreamingAsync] Agent 3f3c9bec6ad54e23858892773a8b536c/MiniCode Invoked client ScriptedChatClient.
```

What `RunStreamingAsync` logs under `[Agent]` for the model's own text, shown here through the same logger `CodingAgent` builds:

```text
[Agent] Task started
[Agent] I've inspected Calculator.cs — Add looks correct.
[Agent] Task completed
```

A complete autonomous run — the syllabus's own example, interleaving all three categories in one real session — needs a live `OPENAI_API_KEY`, since deciding what to inspect and which tools to call is the model's job, not this lesson's code. What reaches the trace is exactly the three captures above, in whatever order the model's own tool calls and text actually produce them.
