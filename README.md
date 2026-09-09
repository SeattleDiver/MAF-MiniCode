# Building a Mini Claude Code–Style Agent with Microsoft Agent Framework

A project-based video course that teaches how to build a practical AI coding agent in C#
using **Microsoft Agent Framework (MAF)** — the successor to Semantic Kernel and AutoGen.

## What is MiniCode?

The course's capstone deliverable is **MiniCode**, a .NET CLI application that resembles a
lightweight version of Claude Code: it can inspect a .NET repository, understand a
developer's request, select and invoke tools, edit source, build the project, run tests,
diagnose failures, correct its own work, and report the changes it made.

```text
cd MyProject
minicode

> Find out why the solution isn't compiling and fix it.
```

The course deliberately uses **one agent only**. There is no manager, planner, coder,
tester, or reviewer agent — a single `CodingAgent` handles the entire workflow through an
iterative tool-calling loop. Multi-agent orchestration is explicitly out of scope; it's
left for a follow-on advanced MAF course.

## Stack

- **Microsoft Agent Framework** (`Microsoft.Agents.AI` and related packages) on **.NET 10**.
- **OpenAI** exclusively, via the first-party `Microsoft.Extensions.AI.OpenAI` `IChatClient`.
- Chat model is always **`gpt-4.1-mini`**. No embeddings, RAG, or vector memory — context
  management is search-and-summarize based (Module 8).
- API key comes from the `OPENAI_API_KEY` environment variable.

## Course Structure

23 lessons plus 4 Phase Capstones = **27 videos**, delivered in five Phases:

| Phase | Modules | Capstone |
|---|---|---|
| 1 — Foundations & Read-Only Agent | 1, 2, 3, 4, 5a, 5b, 6 | **Repository Explorer** (v0.1) |
| 2 — Context & Planning | 7, 8a, 8b, 9 | **Planning Agent** (v0.2) |
| 3 — Taking Action | 10, 11a, 11b, 12, 13 | **Autonomous Fixer** (v0.3) |
| 4 — Governance, CLI & Delivery | 14, 15, 16a, 16b, 17, 18 | **Developer CLI** (v0.4) |
| 5 — Capstone | 19 | Course capstone (v1.0) |

Modules 5, 8, 11, and 16 are split across two lessons each because their topic lists don't
fit a single video's budget; each part shares its Module's syllabus number, scope, and Phase.

By the end, the viewer can: explain how a coding agent differs from a chatbot; build and
configure an agent with MAF; write strongly-typed C# tools; sandbox filesystem access;
manage context without sending the whole repository to the model; produce an explicit,
inspectable task plan before editing; implement safe file edits and shell execution; drive
an autonomous build/test/repair loop; integrate Git; gate destructive operations behind
human approval; and support CLI operating modes (`/plan`, `/diff`, `/test`, `/review`,
`/commit`) on top of the single agent.

The finished agent demonstrates the full arc:

**inspect → plan → search → read → modify → build → test → diagnose → repair → verify → review → report**

`syllabus.md` is the single source of truth for Module numbering, titles, topics, labs, and
scope — see it for full per-Module detail. `video-plan.md` gives the presenting order and
which Modules roll up into which Capstone.

## Repository Layout

Every `ModuleNN-Title/` and `PhaseN-Capstone-Title/` folder is a **complete, standalone,
runnable solution** — the state of MiniCode at the end of that lesson:

```text
ModuleNN-TitleInPascalCase/
    ModuleNN - Title.md              the lesson
    MiniCode.slnx                    a complete solution, at end-of-lesson state
    src/MiniCode.Cli/                terminal interface
    src/MiniCode.Agent/              MAF configuration, agent loop, context, planning
    src/MiniCode.Tools/              tools exposed to the model
    src/MiniCode.Workspace/          repository boundaries and filesystem access
    src/MiniCode.Infrastructure/     shell, Git, config, approval, telemetry
```

Folders are cumulative snapshots: opening `ModuleNN-Title/` alone (no other folder present)
builds and runs. To record Module N, start from Module N−1's folder and build up to what
Module N adds. Phase Capstone folders are full integration checkpoints at that Phase's
version number, introducing no concept absent from `syllabus.md`.

Unit tests are a private verification harness only (`verification/MiniCode.Verify.slnx`,
`verification/MiniCode.Tests/`) — they exist so the presented code can be trusted, but no
test project ever appears in a Module or Capstone folder, and no test code is shown on
camera.

## Status

Per the most recent commit, Phases 1 and 2 are complete; Module 10 (Safe File Editing) is
in progress. `video-plan.md` tracks which videos are built and which remain.
