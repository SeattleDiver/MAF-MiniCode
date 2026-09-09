# MiniCode Build Plan

How the 19 syllabus Modules (23 lessons) accumulate into one working coding agent, and which project owns what. Reference for CLAUDE.md rule 18. syllabus.md wins on any conflict.

## Budgets — the thing that failed before

An earlier attempt at this course produced 1,967 files (82% of them duplicates), 9,484 lines of solution code, 10,482 lines of tests, and lessons averaging 5,000 lines. It failed because every rule was a lower bound — *include this file, cover this case, verify this claim* — and nothing was an upper bound. Keep the budgets in view: **under 300 lines per lesson, at most 125 lines of C# shown, under ~2,500 lines and ~50 files in the finished solution.**

Two habits caused most of the bloat. Watch for both:

- **Result-type hierarchies.** `git status`/`diff`/`log` grew to thirteen types. A small record or a returned string is almost always enough.
- **Scope past the syllabus.** Module 4's topic list says *directory traversal, path normalization*; the earlier version implemented UNC paths, device namespaces, reserved device names and symlink chains. Cover the named topics and stop.

## Project ownership

| Project | Owns | Never contains |
|---|---|---|
| `MiniCode.Cli` | Terminal I/O, streaming render, slash commands, session lifecycle | Business logic, filesystem access, process launching |
| `MiniCode.Agent` | **The central project.** Agent construction, the loop, context, planning | Direct filesystem or process access — it goes through tools |
| `MiniCode.Tools` | Tool definitions, tool dispatch, shaping results for the model | The actual work — tools delegate downward |
| `MiniCode.Workspace` | Path boundaries, file read/write/search, ignore rules | Anything needing a process, network, or model |
| `MiniCode.Infrastructure` | Shell, Git, config, approval, telemetry | Anything the model reasons about directly |

One-way, never cyclic: `Cli → Agent → Tools → { Workspace, Infrastructure }`. The two leaves reference nothing internal.

## Seams to establish early

**Tool dispatch must be interceptable — Module 5a.** Modules 15 and 17 both wrap every tool call (approval, tracing). If tools are handed to MAF raw, both become rewrites. MAF dispatches through the function object it was given, so the seam has to sit *there*: verified previously that `AIFunction.InvokeAsync` is not virtual, the extension point is `protected virtual InvokeCoreAsync`, and `Microsoft.Extensions.AI` ships a concrete `DelegatingAIFunction` to derive from. An interceptor must never throw — with `FunctionInvokingChatClient`'s defaults a throwing tool reaches the model as `Error: Function failed.` and a few in a row end the run.

**The agent owns the conversation — Module 3, exploited in Module 8b.** `CodingAgent` holds the `AgentSession` so compaction can rewrite history without touching the loop. Note `AgentSession` exposes no enumerable history: it is reached via `AgentSessionExtensions.TryGetInMemoryChatHistory` / `SetInMemoryChatHistory`.

**One process gate — Module 11b.** `ShellCommand`'s constructor stays internal so the only way to obtain one is the allow list. Holding a `ShellCommand` *is* evidence of approval, which is why adding Git in Module 14 should be a few strings rather than a new code path.

**One configuration reader.** `CodingAgentFactory` is currently the only caller of `Environment.GetEnvironmentVariable`. Module 18 formalises configuration; nothing else ever reads the environment.

## API facts worth not re-deriving

- `AgentSession` comes from `agent.CreateSessionAsync()`; `AgentThread`/`GetNewThread()` is superseded.
- Non-streaming runs return `AgentResponse` — `AgentRunResponse` no longer exists. Streaming yields `AgentResponseUpdate`.
- Instructions ride on `ChatOptions.Instructions` and are re-sent every request, so compaction cannot drop an `AGENTS.md`.
- MAF 1.20.0 ships `Microsoft.Agents.AI.Compaction`, but its `CompactionMessageIndex` requires a `Microsoft.ML.Tokenizers.Tokenizer` — a dependency this course does not take. Module 8b writes its own.
- `dotnet test --nologo` reports "Zero tests ran" with exit 5 on the .NET 10 SDK. Use plain `dotnet test`.
- xunit.v3 needs `global.json` with `{"test":{"runner":"Microsoft.Testing.Platform"}}` and no `Microsoft.NET.Test.Sdk`.

## Locked shapes

- **`IWorkspace`** (Module 4) — `Root`, `ValidatePath`, `ResolvePath`, `IsPathAllowed`. Nullable path parameters, because from Module 5 the values come from a model. `ValidatePath` never throws and returns a reason; `ResolvePath` throws. Both wrappers delegate to `ValidatePath`, so the decision has one implementation.
- **`ICodingAgent`** (Module 3) — `string` in, `IAsyncEnumerable<string>` out, so no framework type reaches the CLI.

## Progress

| Unit | State | Solution C# | Lesson lines | C# shown |
|---|---|---|---|---|
| Module 1 | complete | — | 176 | — |
| Module 2 | complete | 66 | 195 | 66 |
| Module 3 | complete | 149 | 298 | 115 |
| Module 4 | complete | 274 | 216 | 125 |

Verification harness: `verification/MiniCode.Verify.slnx`, 16 tests passing, covering the sandbox behaviour table published in Module 4.

## Remaining sequence

Module 5a → 5b → 6 → **Phase 1 Capstone (v0.1)** → 7 → 8a → 8b → 9 → **Phase 2 Capstone (v0.2)** → 10 → 11a → 11b → 12 → 13 → **Phase 3 Capstone (v0.3)** → 14 → 15 → 16a → 16b → 17 → 18 → **Phase 4 Capstone (v0.4)** → 19 (v1.0).
