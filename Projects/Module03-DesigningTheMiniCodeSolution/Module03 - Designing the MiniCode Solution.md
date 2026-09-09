# Module 3 — Designing the MiniCode Solution

## Project Overview

We take Module 2's single file and split it across the five projects MiniCode will live in for the rest of the course — then fix the direction dependencies are allowed to flow between them. Nothing new happens on screen: MiniCode still just chats. What changes is that the terminal no longer knows what a model is.

## Prerequisites

**Starting point:** open Module02-MicrosoftAgentFrameworkFundamentals/.

Module 2's four objects — `IChatClient`, `ChatClientAgent`, `AgentSession`, and the streaming loop. That exact code is carried forward here; it just moves out of `Program.cs` and into the projects that own it.

This structure is what **Phase 1 Capstone — Repository Explorer (v0.1)** consolidates.

## Setup

Nothing new to install. The two packages from Module 2 move from `MiniCode.Cli` to `MiniCode.Agent`, which is the whole point of this Module.

## Core Concepts

**Why a solution shape is worth a lesson.** A coding agent edits files and runs commands on your machine because a language model asked it to. The blast radius of a bug is your repository. Every safety claim MiniCode makes later — *it cannot escape the workspace*, *it cannot run an arbitrary command*, *every tool call passes one gate* — is only true if there is exactly **one** code path that can do the dangerous thing. Layering is how you get "exactly one code path." It is a safety mechanism, not decoration.

**The five projects, and what each owns.** These responsibilities are fixed for the rest of the series:

- **MiniCode.Cli** — terminal interface and user interaction.
- **MiniCode.Agent** — MAF configuration and coding-agent execution.
- **MiniCode.Tools** — the tools exposed to the model.
- **MiniCode.Workspace** — repository boundaries and filesystem access.
- **MiniCode.Infrastructure** — shell execution, Git, configuration, external services.

**Three of them are empty today, and that is correct.** `Tools`, `Workspace` and `Infrastructure` exist with their references wired and no code inside — Module 4 fills `Workspace`, Module 5a fills `Tools`, Module 11 fills `Infrastructure`. Creating them now makes the dependency graph real before there is anything to argue about.

**The dependency graph.** Every arrow points downward, and none ever points back up:

```text
  MiniCode.Cli                          front end - no AI packages at all
       |
       v
  MiniCode.Agent                        MAF configuration and execution
       |
       +--------------> MiniCode.Tools  tools exposed to the model
       |                     |
       v                     v
  MiniCode.Workspace                    leaf - repository boundary
  MiniCode.Infrastructure               leaf - shell, Git, config
```

Stated as rules: `Cli` references only `Agent`. `Agent` references `Tools`, `Workspace`, `Infrastructure`. `Tools` references `Workspace` and `Infrastructure`. The two leaves reference nothing internal. Nothing, ever, depends back on `Cli` or `Agent`.

**Why the leaves must stay leaves.** `MiniCode.Workspace` will decide whether a path is inside the repository. `MiniCode.Infrastructure` will decide whether a process starts. If either could reach back up into `Tools` or `Agent`, then answering *"can the agent write to `C:\Windows`?"* would mean reading the whole solution. Because they are leaves, that question is answered by one small class with no dependencies.

**Why the CLI references only the agent.** Look at what `MiniCode.Cli` no longer has: no `Microsoft.Agents.AI`, no OpenAI SDK. It cannot see a `ChatClientAgent`, so it cannot accidentally configure one. `ICodingAgent` is deliberately expressed in `string` and `IAsyncEnumerable<string>` — no framework type crosses that line. The payoff lands in Module 16, when the CLI grows slash commands and operating modes: none of that work can reach the model, and none of the model work can reach the console.

**What actually breaks when the graph goes cyclic.** The failure is gradual, not dramatic. The compiler stops a true cycle outright — C# project references cannot form one — so the cycle never appears *as* a cycle. It appears as a workaround: someone merges two projects, or hoists a type into a shared "Common" project that slowly accumulates everything. Both erase the boundary while leaving the folder names intact, which is worse than an honest cycle, because the diagram still looks right.

## The Code

### `MiniCode.slnx`

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/MiniCode.Agent/MiniCode.Agent.csproj" />
    <Project Path="src/MiniCode.Cli/MiniCode.Cli.csproj" />
    <Project Path="src/MiniCode.Infrastructure/MiniCode.Infrastructure.csproj" />
    <Project Path="src/MiniCode.Tools/MiniCode.Tools.csproj" />
    <Project Path="src/MiniCode.Workspace/MiniCode.Workspace.csproj" />
  </Folder>
</Solution>
```

### `src/MiniCode.Workspace/MiniCode.Workspace.csproj`

A leaf, and you can see it in the file: no `PackageReference`, no `ProjectReference`. `MiniCode.Infrastructure.csproj` is identical apart from its two names.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>MiniCode.Workspace</RootNamespace>
    <AssemblyName>MiniCode.Workspace</AssemblyName>
  </PropertyGroup>

</Project>
```

### `src/MiniCode.Agent/MiniCode.Agent.csproj`

The MAF packages live here now, not in the CLI. `MiniCode.Tools.csproj` follows the same shape with references to the two leaves only, and `MiniCode.Cli.csproj` keeps its `<OutputType>Exe</OutputType>` and drops both packages for a single `ProjectReference` to this project.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>MiniCode.Agent</RootNamespace>
    <AssemblyName>MiniCode.Agent</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Agents.AI" Version="1.20.0" />
    <PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="10.9.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MiniCode.Infrastructure\MiniCode.Infrastructure.csproj" />
    <ProjectReference Include="..\MiniCode.Tools\MiniCode.Tools.csproj" />
    <ProjectReference Include="..\MiniCode.Workspace\MiniCode.Workspace.csproj" />
  </ItemGroup>

</Project>
```

### `src/MiniCode.Agent/ICodingAgent.cs`

```csharp
namespace MiniCode.Agent;

/// <summary>
/// The one thing the terminal is allowed to know about the agent. Deliberately
/// expressed in <see cref="string"/> only, so no framework type crosses into
/// <c>MiniCode.Cli</c>.
/// </summary>
public interface ICodingAgent
{
    /// <summary>Streams the agent's answer to a request, fragment by fragment.</summary>
    IAsyncEnumerable<string> RunStreamingAsync(
        string request,
        CancellationToken cancellationToken = default);
}
```

### `src/MiniCode.Agent/CodingAgent.cs`

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace MiniCode.Agent;

/// <summary>
/// MiniCode's single agent. It owns the MAF <see cref="AIAgent"/> and the
/// <see cref="AgentSession"/> that carries conversation history — owning the
/// session here is what lets Module 8 rewrite that history for long sessions.
/// </summary>
public sealed class CodingAgent : ICodingAgent, IDisposable
{
    private readonly AIAgent _agent;
    private readonly AgentSession _session;
    private readonly IChatClient _chatClient;

    internal CodingAgent(AIAgent agent, AgentSession session, IChatClient chatClient)
    {
        _agent = agent;
        _session = session;
        _chatClient = chatClient;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> RunStreamingAsync(
        string request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (AgentResponseUpdate update in
            _agent.RunStreamingAsync(request, _session, cancellationToken: cancellationToken))
        {
            yield return update.ToString();
        }
    }

    public void Dispose() => _chatClient.Dispose();
}
```

### `src/MiniCode.Agent/CodingAgentFactory.cs`

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace MiniCode.Agent;

/// <summary>
/// The composition root: the one place that names OpenAI, reads the API key, and
/// assembles the agent. Everything above it depends on <see cref="ICodingAgent"/>.
/// </summary>
public static class CodingAgentFactory
{
    private const string ModelId = "gpt-4.1-mini";
    private const string ApiKeyVariable = "OPENAI_API_KEY";

    private const string Instructions =
        "You are MiniCode, a concise assistant for software developers.";

    /// <summary>Builds a ready-to-use agent, throwing if the API key is missing.</summary>
    public static async Task<ICodingAgent> CreateAsync(CancellationToken cancellationToken = default)
    {
        string apiKey = Environment.GetEnvironmentVariable(ApiKeyVariable)
            ?? throw new InvalidOperationException(
                $"{ApiKeyVariable} is not set. Set it before running MiniCode.");

        ChatClient openAiClient = new OpenAIClient(apiKey).GetChatClient(ModelId);
        IChatClient chatClient = openAiClient.AsIChatClient();

        AIAgent agent = new ChatClientAgent(chatClient, Instructions, name: "MiniCode");
        AgentSession session = await agent.CreateSessionAsync(cancellationToken);

        return new CodingAgent(agent, session, chatClient);
    }
}
```

### `src/MiniCode.Cli/Program.cs`

```csharp
// -----------------------------------------------------------------------------
// MiniCode - the terminal front end
//
// Note what this project does NOT reference: no Microsoft.Agents.AI, no OpenAI
// SDK, no filesystem or shell types. One project reference, one contact point.
// -----------------------------------------------------------------------------

using MiniCode.Agent;

namespace MiniCode.Cli;

/// <summary>Entry point. It resolves an agent and hands it to the chat loop.</summary>
internal static class Program
{
    private static async Task<int> Main()
    {
        try
        {
            ICodingAgent agent = await CodingAgentFactory.CreateAsync();
            await new ConsoleChatLoop(agent).RunAsync();
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
```

## Walkthrough

1. **`ICodingAgent` is the whole boundary.** One method, `string` in and `IAsyncEnumerable<string>` out. The CLI cannot name a MAF type because it does not reference the package that defines one.
2. **`CodingAgent` holds the session.** Module 2 created it in `Main`; it now lives on the class that uses it. That relocation is the reason Module 8 can later replace the conversation without touching the loop.
3. **The constructor is `internal`.** The only way to get a `CodingAgent` is through the factory, so nobody can assemble a half-configured one.
4. **`CodingAgentFactory` is the composition root**, and the only place that mentions OpenAI or reads an environment variable. When Module 5a adds tools, they get wired in here.
5. **`Program` shrank to plumbing** — resolve an agent, run the loop, translate a missing key into a message and exit code 1. A misconfigured start now reports to the *person*, not into a model's context. And the two leaf projects compile with no files in them: not a placeholder, but the dependency graph existing before the code does.

## Exercise

**Move the chat loop into its own class.** `Program.cs` above calls `new ConsoleChatLoop(agent).RunAsync()`, and that class does not exist yet. Create `src/MiniCode.Cli/ConsoleChatLoop.cs` and move Module 2's read-evaluate-print loop into it.

Acceptance criteria:

- It takes an `ICodingAgent` in its constructor and nothing else.
- `RunAsync` prints the banner `MiniCode. Type 'exit' to quit.`, then loops: prompt, read a line, stream the answer, blank line.
- Empty input or `exit` (case-insensitive) ends the loop.
- It accepts a `CancellationToken` and stops when cancellation is requested.
- **It compiles with no `using Microsoft.Agents.AI;`.** If you need that using, something has leaked through `ICodingAgent` and the boundary is broken.

The finished version is `src/MiniCode.Cli/ConsoleChatLoop.cs` in **this Module’s folder** — write yours first, then compare.

## Expected Output

Identical behaviour to Module 2, which is the point: a refactor that changes structure and not function.

```text
> dotnet run --project src/MiniCode.Cli

MiniCode. Type 'exit' to quit.

> What is the difference between a record and a class in C#?

A record is a reference type with value-based equality, a compiler-generated
ToString, and concise positional syntax; a class has reference equality unless
you write your own. Use a record when the object is defined by its data.

> exit
```

And with the key unset, the failure now reaches you rather than the model:

```text
> dotnet run --project src/MiniCode.Cli
OPENAI_API_KEY is not set. Set it before running MiniCode.
```
