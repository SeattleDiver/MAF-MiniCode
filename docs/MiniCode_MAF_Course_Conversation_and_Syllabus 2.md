# Mini Claude Code Clone with Microsoft Agent Framework (MAF)

## Conversation and Course Syllabus

**Date:** August 24, 2026

------------------------------------------------------------------------

## Conversation

### User

Can I build a mini Claude code clone with semantic kernel or MAF?

### Assistant

Yes. You can build a very credible **mini Claude Code--style coding
agent in C#**, and I'd use **Microsoft Agent Framework (MAF)** rather
than starting a new implementation directly on Semantic Kernel.

Microsoft now describes Agent Framework as the direct successor to
Semantic Kernel and AutoGen, combining SK's enterprise features with
newer agent and workflow abstractions.

The important distinction is that Claude Code is not primarily a
chatbot. It is essentially an **agent harness around a model**:

``` text
User → coding agent → tools → filesystem/shell/git → observations → agent → repeat
```

That maps extremely well to MAF.

For a first version, I would build something like this:

``` text
MiniCode
│
├── CLI
│   ├── prompt input
│   ├── streaming output
│   └── approval prompts
│
├── CodingAgent
│   ├── system instructions
│   ├── conversation/session
│   ├── tool selection
│   └── iterative agent loop
│
├── Tools
│   ├── ReadFile
│   ├── WriteFile
│   ├── EditFile
│   ├── SearchFiles
│   ├── ListFiles
│   ├── RunCommand
│   ├── GitStatus
│   ├── GitDiff
│   └── GitCommit
│
├── Workspace
│   ├── root-directory sandbox
│   ├── ignore rules
│   └── permissions
│
└── Model Provider
    ├── Azure OpenAI
    ├── OpenAI
    └── potentially Anthropic/other providers
```

The agent loop is surprisingly simple conceptually:

``` csharp
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

MAF handles much of the plumbing around the model, conversations, tool
invocation, sessions, middleware and state.

### The key is your tool layer

Your first coding agent really only needs about **six tools**.

``` csharp
[Description("Read a text file from the workspace")]
public string ReadFile(string path)

[Description("Write a text file in the workspace")]
public void WriteFile(string path, string content)

[Description("Replace text within a file")]
public void EditFile(
    string path,
    string oldText,
    string newText)

[Description("Search workspace files")]
public IEnumerable<SearchResult> Search(
    string pattern)

[Description("List files and directories")]
public IEnumerable<string> ListFiles(
    string path)

[Description("Execute a shell command")]
public async Task<CommandResult> RunCommand(
    string command)
```

With those tools, you can already say:

``` text
> Find the bug causing CustomerService tests to fail.
```

The agent can autonomously do:

``` text
ListFiles()
      ↓
Search("CustomerService")
      ↓
ReadFile("CustomerService.cs")
      ↓
ReadFile("CustomerServiceTests.cs")
      ↓
RunCommand("dotnet test")
      ↓
EditFile(...)
      ↓
RunCommand("dotnet test")
      ↓
GitDiff()
      ↓
Explain changes
```

That is already starting to behave like Claude Code.

### Where it gets interesting

I would **not** initially build a multi-agent system.

Start with one `CodingAgent` and good tools.

Later, MAF can introduce specialized agents, but that is intentionally
outside the scope of the single-agent course.

### One architectural decision matters a lot

Don't give the LLM unrestricted filesystem or shell access.

Put a boundary around the workspace:

``` text
C:\Projects\MyApp
       │
       ├── allowed
       │
       └── agent tools
            │
            X C:\Windows
            X user profile
            X arbitrary processes
```

Classify commands:

``` text
dotnet build       AUTO
dotnet test        AUTO
git status         AUTO
git diff           AUTO
git add            ASK
git commit         ASK
git push           ASK
del / rmdir        ASK
powershell scripts ASK
```

That approval model is one of the biggest differences between a toy
agent and something you can actually trust with a repository.

### Add an AGENTS.md equivalent

At startup:

``` text
/project
   AGENTS.md
   MySolution.sln
   src/
   tests/
```

Example:

``` markdown
# Project Instructions

Framework: .NET 10
Language: C#

Architecture:
- Clean Architecture
- MediatR
- EF Core

Rules:
- Use async APIs.
- Do not modify database migrations.
- Run tests after every code change.
- Never commit automatically.
```

Your agent loads that into its working instructions.

### Realistic Progression

1.  **MiniCode v0.1:** console chat + `ReadFile`, `ListFiles`, `Search`.
2.  **v0.2:** `WriteFile` and deterministic patch/edit operations.
3.  **v0.3:** `dotnet build` and `dotnet test`.
4.  **v0.4:** Git status/diff plus approval system.
5.  **v0.5:** iterative autonomous loop: inspect → modify → compile →
    test → repair.
6.  **v0.6:** repository instructions, context management and
    summarization.
7.  **v0.7:** `/plan`, `/diff`, `/test`, `/review`, `/commit` commands.
8.  **v1:** optional architect/coder/tester/reviewer workflow agents.

For this project, the course stops before v1 and remains a
**single-agent orchestration**.

------------------------------------------------------------------------

### User

Write a course syllabus for this project. Limit it to just a single
agent orchestration. Use MAF.

------------------------------------------------------------------------

# Course Syllabus --- Building a Mini Claude Code--Style Agent with Microsoft Agent Framework

## Course Overview

This project-based course teaches how to build a practical **AI coding
agent in C# using Microsoft Agent Framework (MAF)**.

The finished application will resemble a lightweight version of Claude
Code: a command-line coding assistant capable of examining a .NET
repository, understanding a user's development request, selecting and
invoking tools, modifying source code, building the project, running
tests, inspecting errors, correcting its work, and presenting the final
changes to the developer.

The course deliberately uses **one agent only**.

There is no manager agent, planning agent, coding agent, testing agent,
or reviewer agent. Instead, a single `CodingAgent` uses tools and an
iterative agent loop to accomplish the entire development task.

This keeps the architecture understandable while demonstrating the core
concepts behind production coding agents.

## Learning Objectives

By the end of the course, the student will be able to:

-   Explain how an AI coding agent differs from a chatbot.
-   Build an agent using Microsoft Agent Framework.
-   Connect MAF to an LLM.
-   Create strongly typed C# tools for an AI agent.
-   Allow an agent to inspect a software repository.
-   Implement safe workspace boundaries.
-   Search and read source code efficiently.
-   Implement controlled file editing.
-   Execute `dotnet build` and `dotnet test`.
-   Capture compiler and test output for agent reasoning.
-   Allow an agent to iteratively diagnose and repair code.
-   Integrate Git status and diff operations.
-   Implement human approval for potentially destructive operations.
-   Maintain conversation and task state.
-   Manage repository context without sending the entire repository to
    the model.
-   Stream agent activity to a command-line interface.
-   Build a complete autonomous coding workflow using a single agent.

## Final Capstone

The student will build a .NET CLI application tentatively called
**MiniCode**.

``` bash
cd MyProject
minicode
```

The user can then enter:

``` text
> Find out why the solution isn't compiling and fix it.
```

MiniCode might autonomously perform:

``` text
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

The agent continues working until it either successfully completes the
task, determines it cannot complete the task, or encounters an operation
requiring user approval.

## Module 1 --- Understanding Coding Agents

### Topics

-   Chatbots vs. agents
-   What makes a coding agent different
-   Agentic execution loops
-   LLM reasoning and tool calling
-   Observations and tool results
-   Agent autonomy
-   Human-in-the-loop execution
-   Why Claude Code--style systems are primarily agent harnesses

### Architecture

``` text
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

## Module 2 --- Microsoft Agent Framework Fundamentals

### Topics

-   Microsoft Agent Framework architecture
-   Creating an agent
-   Configuring model providers
-   Agent instructions
-   Sessions
-   Messages
-   Tool/function calling
-   Streaming responses
-   Agent execution lifecycle

### Lab

Create the first MAF console application.

``` text
minicode

> Explain dependency injection in ASP.NET Core.
```

At this stage, the agent is conversational only and cannot access the
filesystem.

## Module 3 --- Designing the MiniCode Solution

``` text
MiniCode.sln

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

-   **MiniCode.Cli:** terminal interface and user interaction.
-   **MiniCode.Agent:** MAF configuration and coding-agent execution.
-   **MiniCode.Tools:** tools exposed to the LLM.
-   **MiniCode.Workspace:** repository boundaries and filesystem
    access.
-   **MiniCode.Infrastructure:** shell execution, Git integration,
    configuration, and external services.

### Lab

Create the complete solution structure and dependency relationships.

## Module 4 --- Building the Workspace Sandbox

Establish boundaries before allowing modifications.

### Topics

-   Workspace root
-   Relative vs. absolute paths
-   Directory traversal
-   Path normalization
-   Allowed directories
-   Ignored directories
-   Binary files
-   `.gitignore`
-   Protecting files outside the repository

### Lab

Implement:

``` csharp
Workspace.ResolvePath()
Workspace.ValidatePath()
Workspace.IsPathAllowed()
```

The agent must be unable to escape its assigned workspace.

## Module 5 --- Giving the Agent Read Access

Implement:

``` text
ListFiles
ReadFile
SearchFiles
```

### Topics

-   MAF tool registration
-   C# function descriptions
-   Tool parameters
-   Tool results
-   Error handling
-   Returning useful information to the model

### Lab

Allow MiniCode to answer questions about an existing .NET repository.

## Module 6 --- Repository Discovery

### Topics

Recognizing:

``` text
*.sln
*.slnx
*.csproj
Directory.Build.props
Directory.Packages.props
global.json
appsettings.json
README.md
```

Identify solution structure, projects, references, tests, target
frameworks, and NuGet dependencies.

### Lab

Ask:

``` text
> Describe the architecture of this solution.
```

MiniCode should inspect the repository and produce an architecture
summary.

## Module 7 --- Project Instructions with AGENTS.md

Example:

``` markdown
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

-   Loading repository instructions
-   Combining system and project instructions
-   Instruction precedence
-   Repository-specific conventions

### Lab

Make MiniCode automatically discover and load `AGENTS.md`.

## Module 8 --- Context Management

### Topics

-   Context windows
-   Token usage
-   Selective file retrieval
-   Search-before-read
-   File-size limits
-   Conversation history
-   Tool-result compression
-   Context relevance

Use:

``` text
Search
   ↓
Identify
   ↓
Read
   ↓
Reason
```

instead of reading the entire repository.

### Lab

Implement context-aware repository exploration.

## Module 9 --- Safe File Editing

Tools:

``` text
WriteFile
EditFile
```

Prefer targeted edits over rewriting complete files whenever possible.

### Topics

-   Deterministic edits
-   Detecting stale source
-   Preventing accidental overwrites
-   File encoding
-   Line endings
-   Creating new files
-   Showing modifications

### Lab

``` text
> Add an EmailAddress property to Customer.
```

The agent must locate the appropriate class and modify it.

## Module 10 --- Shell Command Execution

Tool:

``` text
RunCommand
```

Initially support:

``` bash
dotnet restore
dotnet build
dotnet test
dotnet format
```

### Topics

-   `ProcessStartInfo`
-   stdout
-   stderr
-   exit codes
-   command timeouts
-   cancellation
-   working directories
-   output truncation
-   command allowlists

### Lab

Have the agent build an existing solution and interpret compiler output.

## Module 11 --- The Autonomous Coding Loop

``` text
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

-   Iterative tool execution
-   Agent stopping conditions
-   Failure recovery
-   Maximum iteration limits
-   Detecting repeated failures
-   Preventing infinite loops

### Lab

Intentionally introduce a compilation error and ask:

``` text
> Find and fix the build problem.
```

## Module 12 --- Automated Testing and Self-Correction

``` text
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

-   Unit-test discovery
-   Parsing test output
-   Failed test interpretation
-   Regression detection
-   Test-driven agent behavior

### Lab

Give MiniCode a failing test and have it identify and repair the
underlying defect.

## Module 13 --- Git Integration

Tools:

``` text
GitStatus
GitDiff
GitLog
```

The agent can inspect changes but cannot yet commit them automatically.

### Lab

Have MiniCode generate a final change report based on the Git diff.

## Module 14 --- Human Approval and Safety

Safe operations include:

``` text
ReadFile
ListFiles
SearchFiles
dotnet build
dotnet test
git status
git diff
```

Operations requiring approval include:

``` text
Write outside workspace
Delete files
git commit
git push
Package installation
Unknown shell commands
Destructive commands
```

Example:

``` text
MiniCode wants to execute:

dotnet add package Microsoft.EntityFrameworkCore.SqlServer

Allow?

[Y] Yes
[N] No
[A] Always allow for this session
```

### Lab

Implement an approval middleware/service around tool execution.

## Module 15 --- Building the Developer CLI

Features:

``` text
/help
/status
/files
/diff
/build
/test
/clear
/exit
```

### Topics

-   Streaming output
-   Tool activity display
-   Cancellation
-   CLI state
-   Error presentation
-   Session lifecycle

## Module 16 --- Observability

### Topics

-   Structured logging
-   Agent requests
-   Tool calls
-   Tool duration
-   Token consumption
-   Failed operations
-   Agent iterations
-   Execution traces
-   MAF telemetry

Example:

``` text
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

## Module 17 --- Packaging MiniCode

Target:

``` bash
dotnet tool install -g MiniCode
```

Then:

``` bash
cd CustomerPortal
minicode
```

### Topics

-   .NET global tools
-   Configuration
-   API credentials
-   Model configuration
-   Environment variables
-   NuGet packaging
-   Versioning

### Lab

Package and locally install MiniCode as a .NET CLI tool.

## Module 18 --- Capstone Project

### Assignment

Build a production-quality version of MiniCode capable of completing a
small software-development task autonomously.

Example:

``` text
> Add validation to CustomerService so customers
> without an email address cannot be created.
> Add appropriate unit tests.
```

The single MAF agent should:

1.  Inspect the repository.
2.  Locate CustomerService.
3.  Locate Customer.
4.  Find relevant tests.
5.  Determine the required change.
6.  Modify production code.
7.  Add or modify tests.
8.  Run `dotnet build`.
9.  Fix compilation errors if necessary.
10. Run `dotnet test`.
11. Diagnose failures.
12. Correct the implementation.
13. Run the tests again.
14. Inspect `git diff`.
15. Explain the completed changes.

## Final Architecture

``` text
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

There is **one MAF agent**.

Intelligence remains centralized in the `CodingAgent`; filesystem
access, command execution, Git operations, permissions, and safety are
deterministic C# services exposed as tools.

## Capstone Success Criteria

The project is complete when:

``` bash
cd SomeDotNetSolution
minicode
```

and the developer can request:

``` text
> The CustomerService tests are failing.
  Find the problem and fix it.
```

Without being told which files to open or what commands to execute,
MiniCode should be capable of:

**inspect → reason → modify → build → test → diagnose → correct → verify
→ report**

------------------------------------------------------------------------

### User

Does this syllabus cover everything you outlined above in "a realistic
progression"?

### Assistant

Yes. The syllabus covers essentially **all of the "realistic
progression"**, although several milestones were expanded into their own
modules.

  ---------------------------------------------------------------------------------
  Original progression              Syllabus coverage       Status
  --------------------------------- ----------------------- -----------------------
  **v0.1** Console chat + ReadFile, Modules 2, 5            Covered
  ListFiles, Search                                         

  **v0.2** WriteFile +              Module 9                Covered
  deterministic edits                                       

  **v0.3** `dotnet build` +         Modules 10--12          Covered
  `dotnet test`                                             

  **v0.4** Git status/diff +        Modules 13--14          Covered
  approval system                                           

  **v0.5** Autonomous inspect →     Modules 11--12          Covered
  modify → compile → test → repair                          
  loop                                                      

  **v0.6** Repository               Modules 7--8            Mostly covered
  instructions + context                                    
  management + summarization                                

  **v0.7** `/plan`, `/diff`,        Module 15               Partially covered
  `/test`, `/review`, `/commit`                             

  **v1** Multi-agent                Intentionally excluded  Per course requirement
  architect/coder/tester/reviewer                           
  ---------------------------------------------------------------------------------

Two areas should be strengthened.

### Session Summarization and Context Compaction

Module 8 should explicitly cover long-running sessions where
conversation and tool output exceed useful context.

Topics should include:

-   summarizing completed work,
-   retaining important architectural decisions,
-   preserving modified-file state,
-   retaining unresolved tasks,
-   discarding obsolete tool output,
-   context compaction,
-   restoring working state after compaction.

### Complete CLI Operating Modes

The CLI should ultimately include:

``` text
/plan
/diff
/test
/review
/commit
```

`/plan` should allow the same single agent to create an implementation
plan without changing files.

Example:

``` text
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

`/review` should place the same agent into a read-only review mode:

``` text
> /review
```

The agent examines the current Git diff for correctness problems,
regressions, security issues, unnecessary changes, and missing tests
without modifying files.

### Add Explicit Planning and Task State

The course should also explicitly teach planning and task state before
the autonomous coding loop:

``` text
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

The desired final progression is therefore:

**MAF basics → tools → repository understanding → context → planning →
editing → execution → autonomous loop → self-correction → Git → safety →
CLI modes → observability → packaging → capstone.**

The course remains **100% single-agent MAF**. Multi-agent orchestration
is intentionally excluded and can be treated as a follow-on course.

------------------------------------------------------------------------

## Recommended Final Course Scope

The completed course should therefore cover the following progression:

### v0.1 --- Read-Only Coding Assistant

-   MAF console agent
-   `ReadFile`
-   `ListFiles`
-   `SearchFiles`
-   Repository inspection

### v0.2 --- Code Modification

-   `WriteFile`
-   `EditFile`
-   Deterministic patches
-   Workspace sandbox

### v0.3 --- Build and Test

-   `RunCommand`
-   `dotnet restore`
-   `dotnet build`
-   `dotnet test`
-   Compiler/test-output interpretation

### v0.4 --- Git and Safety

-   `GitStatus`
-   `GitDiff`
-   `GitLog`
-   Approval policies
-   Command allowlists
-   Human-in-the-loop execution

### v0.5 --- Autonomous Coding Loop

-   Inspect
-   Reason
-   Modify
-   Build
-   Test
-   Diagnose
-   Repair
-   Verify
-   Stop conditions

### v0.6 --- Repository Intelligence and Context

-   `AGENTS.md`
-   Repository instructions
-   Context selection
-   Search-before-read
-   Tool-result compression
-   Session summarization
-   Context compaction
-   State preservation

### v0.7 --- Developer Experience

-   `/plan`
-   `/diff`
-   `/test`
-   `/review`
-   `/commit`
-   Streaming CLI
-   Task planning
-   Task-state tracking
-   Read-only operating modes
-   Approval modes

### Capstone --- MiniCode

A single Microsoft Agent Framework coding agent capable of autonomously:

**inspect → plan → search → read → modify → build → test → diagnose →
repair → verify → review → report**

This is the intended endpoint of the project.

------------------------------------------------------------------------

## Scope Boundary

This course intentionally does **not** introduce:

-   Manager agents
-   Architect agents
-   Separate coding agents
-   Test agents
-   Reviewer agents
-   Agent-to-agent communication
-   Multi-agent workflows

Those concepts belong in a subsequent advanced MAF course.

The objective here is to fully understand and implement the mechanics of
a capable coding agent before introducing multi-agent orchestration.
