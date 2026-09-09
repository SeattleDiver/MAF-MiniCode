# Module 1 — Understanding Coding Agents

## Project Overview

Before writing a single line of Microsoft Agent Framework code, we need a shared mental model for what we're actually building. MiniCode is not a chatbot with a terminal skin — it's an **agent harness**: a loop that lets a language model reason, call tools, observe results, and keep going until a development task is done. This Module builds that mental model and produces the first real artifact of the course: a high-level architecture design for MiniCode.

## Prerequisites

None. This is the first Module in the course.

This Module produces a design document rather than code, so there is no solution folder here — no `MiniCode.slnx`, no `.csproj`. The architecture it defines is what Modules 2–6 implement and what the **Phase 1 Capstone — Repository Explorer (v0.1)** assembles and demonstrates end to end.

## Setup

No packages, SDKs, or environment variables are required for this Module. We are not writing or running code yet — Module 2 introduces the first MAF console application. This Module is entirely conceptual and design-focused.

## Core Concepts

### Chatbots vs. Agents

A chatbot answers a message and stops. You send text, it sends text back, and the interaction ends until you send the next message. Everything the chatbot needs to answer is already inside the conversation you gave it.

An agent is different in one crucial way: **it can act on the world, observe what happened, and decide what to do next — without you telling it each step.** Ask a chatbot "why is the build failing?" and it can only guess based on what you paste in. Ask an agent the same question, and it can run the build itself, read the error output, open the offending file, and tell you exactly what's wrong — possibly after several rounds of investigation you never had to script.

### What Makes a Coding Agent Different

A general-purpose agent might book a flight or query a database. A **coding agent** operates specifically over a software repository: it reads source code, understands project structure, executes build and test tooling, edits files, and inspects version control. The tools are different, but the underlying mechanism — reason, act, observe, repeat — is the same one this whole course is built around.

### Agentic Execution Loops

This is the mechanical heart of every agent, including MiniCode. Structurally, an agentic loop looks like this:

```csharp
while (!completed)
{
    var response = await agent.RunAsync(messages);

    if (response.HasToolCalls)
    {
        foreach (var call in response.ToolCalls)
        {
            var result = await tools.ExecuteAsync(call);
            messages.Add(result);
        }
    }
    else
    {
        completed = true;
    }
}
```

Each pass through the loop, the model either asks for a tool to be run, or produces a final answer. As long as it keeps requesting tools, the loop keeps running. This is the difference between a single request/response call to an LLM and an agent: the loop, not the model, is what makes multi-step autonomous work possible. We won't write this loop for real until Module 12, but every Module between now and then exists to give the loop something useful to do.

### LLM Reasoning and Tool Calling

Modern chat models can do more than generate text — given a list of available functions (name, description, parameter schema), they can decide *which* function is relevant to the current problem and *what arguments* to call it with. The model doesn't execute the function itself; it emits a structured request. Your code is responsible for actually running it. This "reasoning about which tool to use" step is what lets a single instruction like "fix the build" turn into a sequence of concrete actions: list files, search for a symbol, read a file, run a compiler.

### Observations and Tool Results

After a tool runs, its result — file contents, compiler output, a list of matching files — is fed back into the conversation as an observation. The model reads that observation the same way it reads anything else in the conversation, and reasons about what to do next. This observe-then-reason step is what allows an agent to *adapt*: if `dotnet build` fails, the resulting error text becomes the input to the next reasoning step, steering the agent toward reading the file the error points to.

### Agent Autonomy

Autonomy is a spectrum, not a switch. An agent can be scoped anywhere from "suggest one edit and stop" to "keep working, unsupervised, until the task is fully done or it hits a wall." MiniCode sits toward the autonomous end: given a request like "find out why the solution isn't compiling and fix it," it should be able to chain together file discovery, reading, editing, building, and testing on its own. But full autonomy over a real codebase is risky without limits — which is exactly why later Modules introduce workspace sandboxing (Module 4) and human approval for dangerous operations (Module 15).

### Human-in-the-Loop Execution

Autonomy doesn't mean the human disappears. A well-designed coding agent knows which actions are safe to take on its own (reading a file, running `dotnet build`) and which require a person to explicitly approve them (deleting a file, running `git commit`, installing a package). This course treats human-in-the-loop approval as a first-class architectural concern, not an afterthought — it's the difference between a demo and something you'd actually let run against a real repository.

### Why Claude Code–Style Systems Are Primarily Agent Harnesses

It's tempting to think a tool like Claude Code is "just a really good model." In practice, most of its capability comes from the **harness** surrounding the model: the tool definitions, the workspace boundaries, the approval policy, the context management strategy, the loop that keeps it going until the task is done. The model supplies reasoning; the harness supplies structure, safety, and persistence. MiniCode is our chance to build that harness ourselves, in C#, on top of Microsoft Agent Framework, and understand exactly what it's doing and why.

## Architecture

Every Module in this course is an elaboration of this one loop:

```text
User
 │
 ▼
Coding Agent
 │
 ▼
LLM
 │
 ▼
Tool Request
 │
 ▼
C# Tool
 │
 ▼
Tool Result
 │
 ▼
Coding Agent
 │
 └──────────► Repeat
```

The user issues a request once. From there, the Coding Agent and the LLM cycle through tool requests and tool results — potentially many times — without further input, until the LLM produces a final answer instead of another tool request.

## Lab: Design the High-Level Architecture for MiniCode

This Module's deliverable isn't code — it's a design. Below is the architecture MiniCode will be built toward over the rest of the course. Treat this as the reference diagram you'll keep returning to.

```text
                 Developer
                     │
                     ▼
               MiniCode CLI
                     │
                     ▼
              ┌──────────────┐
              │  MAF Coding  │
              │    Agent     │
              └──────┬───────┘
                     │
              Tool Invocation
                     │
        ┌────────────┼────────────┐
        │            │            │
        ▼            ▼            ▼
    Workspace      Shell         Git
        │            │            │
   ┌────┼────┐       │       ┌────┼────┐
   │    │    │       │       │    │    │
 Read Search Edit   Build   Status Diff Log
                    Test
        │            │            │
        └────────────┼────────────┘
                     │
                     ▼
               .NET Repository
```

**Component responsibilities:**

| Component | Owning project | Responsibility |
|---|---|---|
| **MiniCode CLI** | `MiniCode.Cli` | Terminal interface — accepts developer requests, streams agent activity, prompts for approvals. Holds no business logic. |
| **MAF Coding Agent** | `MiniCode.Agent` | The single agent (`CodingAgent`) that owns instructions, reasoning, tool selection, and the execution loop. There is only ever one agent — no manager, planner, or reviewer agents. It never touches the filesystem or a process directly; it goes through tools. |
| **Tool Invocation** | `MiniCode.Tools` | Defines each capability as a tool the model can request, dispatches the call, and shapes the result into text the model can reason about. The tools themselves do no real work — they delegate to Workspace and Infrastructure. |
| **Workspace** | `MiniCode.Workspace` | The sandbox boundary: path validation scoped to the repository root, plus the file read/search/edit primitives, ignore rules, and binary detection the Read/Search/Edit tools call into. |
| **Shell** | `MiniCode.Infrastructure` | Runs allowlisted commands (`dotnet build`, `dotnet test`, etc.) with timeouts and cancellation, capturing stdout, stderr, and exit codes. |
| **Git** | `MiniCode.Infrastructure` | Supplies status, diff, and log for inspection. Approval policy — which also lives here — gates anything that would mutate the repository or its history. |

Two of the boxes in the diagram share a project: Shell and Git are both external processes MiniCode drives on the developer's behalf, so they live together in `MiniCode.Infrastructure` alongside configuration, approval policy, and logging. That is five projects in total, which is the whole of MiniCode.

This diagram maps directly onto the solution structure we will create in Module 3 (`MiniCode.Cli`, `MiniCode.Agent`, `MiniCode.Tools`, `MiniCode.Workspace`, `MiniCode.Infrastructure`), and every Module after that fills in one more piece of it — we are building toward this single diagram for the rest of the course, not starting over each time. Dependencies only ever point one way, and never cycle:

```text
Cli → Agent → Tools → { Workspace, Infrastructure }
```

`Workspace` and `Infrastructure` are the leaves: they reference none of the other projects, and nothing ever depends back on `Cli` or `Agent`.

## Walkthrough

1. **Start from the loop, not the diagram.** The box-and-arrow architecture above is just the execution loop from Core Concepts, drawn with concrete MiniCode nouns substituted in: "LLM" becomes "MAF Coding Agent," "Tool Request/Result" becomes the Workspace/Shell/Git tool categories.
2. **Notice there's exactly one agent.** Every arrow in the diagram terminates back at the single `CodingAgent` box. There's no second agent reviewing its work or a third planning ahead. Intelligence stays centralized in the `CodingAgent`; filesystem access, command execution, Git operations, permissions, and safety are all deterministic C# services exposed to it as tools. This is the design premise of the entire course — no Module from here to the capstone introduces a second agent.
3. **Notice the tools are grouped by concern, not by Module.** Read/Search/Edit belong to the Workspace; Build/Test belong to Shell; Status/Diff/Log belong to Git. That grouping is what the Module 3 solution structure follows — with one wrinkle worth internalizing now: the *tool definitions* live together in `MiniCode.Tools`, while the *work* lives in the project that owns the concern. `ReadFile` is declared in `MiniCode.Tools` and delegates to `MiniCode.Workspace`; `RunCommand` is declared in `MiniCode.Tools` and delegates to `MiniCode.Infrastructure`. Keeping the tool surface separate from the implementation is what lets Modules 15 and 17 wrap every tool call with approval and tracing without reopening a single tool class.
4. **The sandbox boundary is implicit here and explicit later.** This diagram doesn't yet show *how* the agent is prevented from writing outside the repository or running a destructive shell command — that's Module 4 (Workspace Sandbox) and Module 15 (Human Approval and Safety). For now, just note that every arrow into Workspace/Shell/Git is a place where a boundary will eventually be enforced.
5. **The diagram has no context-management or planning boxes yet.** That's intentional — Modules 8 and 9 add explicit context compaction and task-plan state on top of this same loop, rather than replacing it.

## Expected Output

By the end of this Module, you should have:

- A clear, articulable answer to "what's the difference between a chatbot and an agent?" grounded in the reason → act → observe → repeat loop.
- The MiniCode architecture diagram above, understood well enough to redraw from memory with the correct component responsibilities.
- An explicit understanding that MiniCode is a **single-agent harness** — no multi-agent orchestration will appear anywhere in this course.

No code runs in this Module, so there is no build or console output to verify — the deliverable is the design artifact itself, which we'll start implementing in Module 2.
