# Course Syllabus — Building a Mini Claude Code–Style Agent with Microsoft Agent Framework

*Consolidated from `MiniCode_MAF_Course_Conversation_and_Syllabus 2.md`, incorporating the follow-up revisions (session summarization/context compaction, `/plan` and `/review` operating modes, and explicit planning/task state) into the module structure.*

## Course Overview

This project-based course teaches how to build a practical **AI coding agent in C# using Microsoft Agent Framework (MAF)**.

The finished application will resemble a lightweight version of Claude Code: a command-line coding assistant capable of examining a .NET repository, understanding a user's development request, selecting and invoking tools, modifying source code, building the project, running tests, inspecting errors, correcting its work, and presenting the final changes to the developer.

The course deliberately uses **one agent only**. There is no manager agent, planning agent, coding agent, testing agent, or reviewer agent. Instead, a single `CodingAgent` uses tools and an iterative agent loop to accomplish the entire development task. This keeps the architecture understandable while demonstrating the core concepts behind production coding agents.

## Learning Objectives

By the end of the course, the student will be able to:

- Explain how an AI coding agent differs from a chatbot.
- Build an agent using Microsoft Agent Framework.
- Connect MAF to an LLM.
- Create strongly typed C# tools for an AI agent.
- Allow an agent to inspect a software repository.
- Implement safe workspace boundaries.
- Search and read source code efficiently.
- Manage repository context without sending the entire repository to the model, including summarizing completed work and compacting long-running sessions.
- Create an explicit task plan and track task state before making changes.
- Implement controlled file editing.
- Execute `dotnet build` and `dotnet test`.
- Capture compiler and test output for agent reasoning.
- Allow an agent to iteratively diagnose and repair code.
- Integrate Git status, diff, and log operations.
- Implement human approval for potentially destructive operations.
- Maintain conversation and task state.
- Stream agent activity to a command-line interface.
- Support distinct CLI operating modes (`/plan`, `/diff`, `/test`, `/review`, `/commit`) on top of a single agent.
- Build a complete autonomous coding workflow using a single agent.

## Final Capstone

The student will build a .NET CLI application tentatively called **MiniCode**.

```bash
cd MyProject
minicode
```

The user can then enter:

```text
> Find out why the solution isn't compiling and fix it.
```

MiniCode might autonomously perform:

```text
User Request
     │
     ▼
CodingAgent
     │
     ├── ListFiles
     ├── SearchFiles
     ├── ReadFile
     ├── RunCommand("dotnet build")
     ├── ReadFile
     ├── EditFile
     ├── RunCommand("dotnet build")
     ├── RunCommand("dotnet test")
     ├── GitDiff
     └── Final Response
```

The agent continues working until it either successfully completes the task, determines it cannot complete the task, or encounters an operation requiring user approval.

## Module Progression at a Glance

**MAF basics → tools → repository understanding → context → planning → editing → execution → autonomous loop → self-correction → Git → safety → CLI modes → observability → packaging → capstone.**

| Phase | # | Module | Corresponds to prior "v" milestone |
|-------|---|--------|-------------------------------------|
| 1 — Foundations & Read-Only Agent | 1 | Understanding Coding Agents | — |
| 1 | 2 | Microsoft Agent Framework Fundamentals | v0.1 |
| 1 | 3 | Designing the MiniCode Solution | — |
| 1 | 4 | Building the Workspace Sandbox | v0.1 |
| 1 | 5a | Giving the Agent Read Access - The Interception Seam | v0.1 |
| 1 | 5b | Giving the Agent Read Access - Reading Files | v0.1 |
| 1 | 6 | Repository Discovery | v0.1 |
| 1 | ★ | **Phase 1 Capstone — Repository Explorer** | v0.1 |
| 2 — Context & Planning | 7 | Project Instructions with AGENTS.md | v0.2 |
| 2 | 8a | Context Management - Budget and Search Before Read | v0.2 |
| 2 | 8b | Context Management - Working State and Compaction | v0.2 |
| 2 | 9 | Planning and Task State | v0.2 |
| 2 | ★ | **Phase 2 Capstone — Planning Agent** | v0.2 |
| 3 — Taking Action | 10 | Safe File Editing | v0.3 |
| 3 | 11a | Shell Command Execution - Running a Process Safely | v0.3 |
| 3 | 11b | Shell Command Execution - The Command Allow List | v0.3 |
| 3 | 12 | The Autonomous Coding Loop | v0.3 |
| 3 | 13 | Automated Testing and Self-Correction | v0.3 |
| 3 | ★ | **Phase 3 Capstone — Autonomous Fixer** | v0.3 |
| 4 — Governance, CLI & Delivery | 14 | Git Integration | v0.4 |
| 4 | 15 | Human Approval and Safety | v0.4 |
| 4 | 16a | Developer CLI - Dispatch and Informational Commands | v0.4 |
| 4 | 16b | Developer CLI - Plan and Review Modes | v0.4 |
| 4 | 17 | Observability | — |
| 4 | 18 | Packaging MiniCode | — |
| 4 | ★ | **Phase 4 Capstone — Developer CLI** | v0.4 |
| 5 — Capstone | 19 | Capstone Project | v1.0 |

★ Phase Capstones are integration checkpoints, not numbered Modules. They assemble the work of the preceding Modules into the complete MiniCode solution and introduce no new concepts.

## Course Delivery Structure

There is **one** MiniCode solution, at the repository root, and it grows as the series progresses. The viewer clones the repository once and follows along; they never retype earlier work and never copy a folder.

A Module is delivered as a folder containing **only its lesson** — `ModuleNN-TitleInPascalCase/ModuleNN - Title.md`. Module folders hold no `.slnx`, no `.csproj` and no source, because the source lives in the one solution at the root. No Module's code depends on anything a later Module introduces.

The `PhaseN-Capstone-Title` folders are the **only** full snapshots: each holds a complete, runnable copy of the solution as that Phase left it, so a viewer joining mid-series can start there, and exercises the tool end-to-end at the stated version number.

### Lesson format

Each lesson is sized for a single video:

- **Under 300 lines** of written material.
- **At most 125 lines of C# shown** — what can be presented at a reasonable pace on camera.
- **Core Concepts is written to work as a slide** — the idea in plain language, no code, presented away from the editor.
- **Whatever exceeds the code budget becomes an Exercise** with acceptance criteria stated in prose. The reference implementation is in the root solution, so a viewer can compare their attempt against it.
- **Each Module demonstrates the happy path.** Failure modes get a sentence where they explain a design choice; the demonstration is the success case.

**Unit tests are not part of the course.** A private verification harness exists so the presented code can be trusted, but no test code appears in any lesson and no test project appears in `MiniCode.slnx` or in a Capstone snapshot.

### Modules split across two videos

Four Modules cannot fit their topic list inside one lesson's budget and are delivered as two parts each. Parts share the Module's syllabus number, scope and Phase, so the 19-Module structure and the Phase mapping are unchanged:

| Module | Part a | Part b |
|---|---|---|
| 5 — Giving the Agent Read Access | the tool-interception seam and tool registration | `ListFiles` and `ReadFile` |
| 8 — Context Management | token budget and search-before-read | working state and compaction |
| 11 — Shell Command Execution | running a process safely | the command allow list |
| 16 — Developer CLI and Operating Modes | command dispatch and informational commands | `/plan` and `/review` read-only modes |

**23 lessons plus 4 Phase Capstones = 27 videos.**

```text
D:\MAF-MiniCode\
   CLAUDE.md
   syllabus.md
   docs\
   MiniCode.slnx                                the one solution a viewer opens
   global.json
   AGENTS.md
   src\MiniCode.Cli\ .Agent\ .Tools\ .Workspace\ .Infrastructure\
   verification\                                our harness - never presented
        MiniCode.Verify.slnx
        MiniCode.Tests\
   Module01-UnderstandingCodingAgents\
        Module01 - Understanding Coding Agents.md
   Module02-MicrosoftAgentFrameworkFundamentals\
        Module02 - Microsoft Agent Framework Fundamentals.md
   ...                                          lesson only, no code
   Phase1-Capstone-RepositoryExplorer\
        MiniCode.slnx
        src\MiniCode.Cli\ .Agent\ .Tools\ .Workspace\ .Infrastructure\
        Phase1 Capstone - Repository Explorer.md
   ...
   Module19-CapstoneProject\
```

## Module 1 — Understanding Coding Agents

### Topics

- Chatbots vs. agents
- What makes a coding agent different
- Agentic execution loops
- LLM reasoning and tool calling
- Observations and tool results
- Agent autonomy
- Human-in-the-loop execution
- Why Claude Code–style systems are primarily agent harnesses

### Architecture

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

### Lab

Design the high-level architecture for MiniCode.

## Module 2 — Microsoft Agent Framework Fundamentals

### Topics

- Microsoft Agent Framework architecture
- Creating an agent
- Configuring model providers
- Agent instructions
- Sessions
- Messages
- Tool/function calling
- Streaming responses
- Agent execution lifecycle

### Lab

Create the first MAF console application.

```text
minicode

> Explain dependency injection in ASP.NET Core.
```

At this stage, the agent is conversational only and cannot access the filesystem.

## Module 3 — Designing the MiniCode Solution

```text
MiniCode.slnx

src/
   MiniCode.Cli/
   MiniCode.Agent/
   MiniCode.Tools/
   MiniCode.Workspace/
   MiniCode.Infrastructure/

tests/
   MiniCode.Tests/
```

Responsibilities:

- **MiniCode.Cli:** terminal interface and user interaction.
- **MiniCode.Agent:** MAF configuration and coding-agent execution.
- **MiniCode.Tools:** tools exposed to the LLM.
- **MiniCode.Workspace:** repository boundaries and filesystem access.
- **MiniCode.Infrastructure:** shell execution, Git integration, configuration, and external services.

### Lab

Create the complete solution structure and dependency relationships.

## Module 4 — Building the Workspace Sandbox

Establish boundaries before allowing modifications.

### Topics

- Workspace root
- Relative vs. absolute paths
- Directory traversal
- Path normalization
- Allowed directories
- Ignored directories
- Binary files
- `.gitignore`
- Protecting files outside the repository

### Lab

Implement:

```csharp
Workspace.ResolvePath()
Workspace.ValidatePath()
Workspace.IsPathAllowed()
```

The agent must be unable to escape its assigned workspace.

## Module 5 — Giving the Agent Read Access

Implement:

```text
ListFiles
ReadFile
SearchFiles
```

### Topics

- MAF tool registration
- C# function descriptions
- Tool parameters
- Tool results
- Error handling
- Returning useful information to the model

### Lab

Allow MiniCode to answer questions about an existing .NET repository.

## Module 6 — Repository Discovery

### Topics

Recognizing:

```text
*.sln
*.slnx
*.csproj
Directory.Build.props
Directory.Packages.props
global.json
appsettings.json
README.md
```

Identify solution structure, projects, references, tests, target frameworks, and NuGet dependencies.

### Lab

Ask:

```text
> Describe the architecture of this solution.
```

MiniCode should inspect the repository and produce an architecture summary.

## Module 7 — Project Instructions with AGENTS.md

Example:

```markdown
# Development Instructions

Platform: .NET 10

Architecture:
- Clean Architecture
- ASP.NET Core
- EF Core

Rules:
- Use async APIs.
- Use dependency injection.
- Do not modify migrations.
- Add tests for new functionality.
- Run tests after changing code.
- Never commit automatically.
```

### Topics

- Loading repository instructions
- Combining system and project instructions
- Instruction precedence
- Repository-specific conventions

### Lab

Make MiniCode automatically discover and load `AGENTS.md`.

## Module 8 — Context Management, Summarization, and Compaction

### Topics

- Context windows
- Token usage
- Selective file retrieval
- Search-before-read
- File-size limits
- Conversation history
- Tool-result compression
- Context relevance
- Summarizing completed work
- Retaining important architectural decisions
- Preserving modified-file state
- Retaining unresolved tasks
- Discarding obsolete tool output
- Context compaction
- Restoring working state after compaction

Use:

```text
Search
   ↓
Identify
   ↓
Read
   ↓
Reason
```

instead of reading the entire repository. For long-running sessions, periodically compact the conversation: summarize completed work, keep track of modified files and unresolved tasks, and discard tool output that is no longer relevant.

### Lab

Implement context-aware repository exploration, then simulate a long-running session and implement a compaction routine that preserves task state across the compaction boundary.

## Module 9 — Planning and Task State

Before the agent begins editing files, it should convert a user request into an explicit, inspectable plan.

```text
User Request
     │
     ▼
Understand Goal
     │
     ▼
Create Task Plan
     │
     ▼
┌─────────────────────┐
│ □ Inspect project   │
│ □ Locate code       │
│ □ Implement change  │
│ □ Build             │
│ □ Test              │
│ □ Review diff       │
└─────────────────────┘
     │
     ▼
Execute
     │
     ▼
Update Plan
     │
     ▼
Continue / Replan
```

### Topics

- Translating a user request into a discrete task plan
- Representing task state (pending, in-progress, complete, blocked)
- Updating the plan as new information is discovered
- Replanning after unexpected build/test results
- Presenting a plan to the user without modifying files (foundation for the `/plan` CLI mode covered in Module 16)

### Lab

```text
> /plan Add caching to CustomerService

Agent:
1. Inspect CustomerService
2. Identify existing caching infrastructure
3. Determine appropriate cache lifetime
4. Modify service
5. Add tests
6. Build
7. Test

No files have been modified.
```

Implement task-plan generation and task-state tracking without allowing any file modification.

## Module 10 — Safe File Editing

Tools:

```text
WriteFile
EditFile
```

Prefer targeted edits over rewriting complete files whenever possible.

### Topics

- Deterministic edits
- Detecting stale source
- Preventing accidental overwrites
- File encoding
- Line endings
- Creating new files
- Showing modifications

### Lab

```text
> Add an EmailAddress property to Customer.
```

The agent must locate the appropriate class and modify it.

## Module 11 — Shell Command Execution

Tool:

```text
RunCommand
```

Initially support:

```bash
dotnet restore
dotnet build
dotnet test
dotnet format
```

### Topics

- `ProcessStartInfo`
- stdout
- stderr
- exit codes
- command timeouts
- cancellation
- working directories
- output truncation
- command allowlists

### Lab

Have the agent build an existing solution and interpret compiler output.

## Module 12 — The Autonomous Coding Loop

```text
User
 │
 │ "Fix the build."
 ▼
CodingAgent
 │
 ├── Inspect repository
 ├── Run dotnet build
 ├── Examine errors
 ├── Search code
 ├── Read relevant files
 ├── Edit code
 ├── Run dotnet build
 └── Evaluate result
        │
        ├── FAILED ──► continue
        └── SUCCESS
```

### Topics

- Iterative tool execution
- Agent stopping conditions
- Failure recovery
- Maximum iteration limits
- Detecting repeated failures
- Preventing infinite loops

### Lab

Intentionally introduce a compilation error and ask:

```text
> Find and fix the build problem.
```

## Module 13 — Automated Testing and Self-Correction

```text
Modify Code
     ↓
dotnet build
     ↓
dotnet test
     ↓
Tests Pass?
   /     \
 NO      YES
 │        │
 ▼        ▼
Diagnose  Finish
 │
 ▼
Modify
 │
 └────────► Test Again
```

### Topics

- Unit-test discovery
- Parsing test output
- Failed test interpretation
- Regression detection
- Test-driven agent behavior

### Lab

Give MiniCode a failing test and have it identify and repair the underlying defect.

## Module 14 — Git Integration

Tools:

```text
GitStatus
GitDiff
GitLog
```

The agent can inspect changes but cannot yet commit them automatically.

### Lab

Have MiniCode generate a final change report based on the Git diff.

## Module 15 — Human Approval and Safety

Safe operations include:

```text
ReadFile
ListFiles
SearchFiles
dotnet build
dotnet test
git status
git diff
```

Operations requiring approval include:

```text
Write outside workspace
Delete files
git commit
git push
Package installation
Unknown shell commands
Destructive commands
```

Example:

```text
MiniCode wants to execute:

dotnet add package Microsoft.EntityFrameworkCore.SqlServer

Allow?

[Y] Yes
[N] No
[A] Always allow for this session
```

### Lab

Implement an approval middleware/service around tool execution.

## Module 16 — Building the Developer CLI and Operating Modes

Features:

```text
/help
/status
/files
/diff
/build
/test
/plan
/review
/commit
/clear
/exit
```

### Topics

- Streaming output
- Tool activity display
- Cancellation
- CLI state
- Error presentation
- Session lifecycle
- `/plan` mode: generate a task plan (Module 9) without modifying files
- `/review` mode: read-only inspection of the current Git diff for correctness problems, regressions, security issues, unnecessary changes, and missing tests
- `/diff`, `/test`, `/commit` as explicit, user-invoked operating modes layered on the single agent

### Lab

```text
> /review
```

Implement `/plan` and `/review` as read-only operating modes, and wire `/diff`, `/test`, and `/commit` to the existing tools and approval policies from Modules 11–15.

## Module 17 — Observability

### Topics

- Structured logging
- Agent requests
- Tool calls
- Tool duration
- Token consumption
- Failed operations
- Agent iterations
- Execution traces
- MAF telemetry

Example:

```text
Task started

[Agent] Inspect repository
[Tool] SearchFiles
[Tool] ReadFile
[Agent] Suspect null handling defect
[Tool] EditFile
[Tool] dotnet build
[Result] Success
[Tool] dotnet test
[Result] 42 passed

Task completed
```

### Lab

Add structured tracing to a complete agent run.

## Module 18 — Packaging MiniCode

Target:

```bash
dotnet tool install -g MiniCode
```

Then:

```bash
cd CustomerPortal
minicode
```

### Topics

- .NET global tools
- Configuration
- API credentials
- Model configuration
- Environment variables
- NuGet packaging
- Versioning

### Lab

Package and locally install MiniCode as a .NET CLI tool.

## Module 19 — Capstone Project

### Assignment

Build a production-quality version of MiniCode capable of completing a small software-development task autonomously.

Example:

```text
> Add validation to CustomerService so customers
> without an email address cannot be created.
> Add appropriate unit tests.
```

The single MAF agent should:

1. Inspect the repository.
2. Locate CustomerService.
3. Locate Customer.
4. Find relevant tests.
5. Determine the required change.
6. Modify production code.
7. Add or modify tests.
8. Run `dotnet build`.
9. Fix compilation errors if necessary.
10. Run `dotnet test`.
11. Diagnose failures.
12. Correct the implementation.
13. Run the tests again.
14. Inspect `git diff`.
15. Explain the completed changes.

## Final Architecture

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

There is **one MAF agent**. Intelligence remains centralized in the `CodingAgent`; filesystem access, command execution, Git operations, permissions, and safety are deterministic C# services exposed as tools.

## Capstone Success Criteria

The project is complete when:

```bash
cd SomeDotNetSolution
minicode
```

and the developer can request:

```text
> The CustomerService tests are failing.
  Find the problem and fix it.
```

Without being told which files to open or what commands to execute, MiniCode should be capable of:

**inspect → plan → search → read → modify → build → test → diagnose → repair → verify → review → report**

## Scope Boundary

This course intentionally does **not** introduce:

- Manager agents
- Architect agents
- Separate coding agents
- Test agents
- Reviewer agents
- Agent-to-agent communication
- Multi-agent workflows

Those concepts belong in a subsequent advanced MAF course. The objective here is to fully understand and implement the mechanics of a capable coding agent before introducing multi-agent orchestration.
