# Module 2 — Microsoft Agent Framework Fundamentals

## Project Overview

We build the first running piece of MiniCode: a console app that connects to OpenAI through Microsoft Agent Framework and streams replies back as they arrive. It has no tools and no filesystem access yet — the point is to get a real MAF agent talking, and to see the four objects every later Module builds on.

## Prerequisites

**Starting point:** an empty folder. This is the first code Module.


Module 1's mental model — the reason → act → observe loop, and the idea that a coding agent is mostly harness rather than model. Nothing else. This is the first code Module, so there is nothing carried forward.

This code becomes the CLI layer of the solution designed in Module 3, and ships as part of **Phase 1 Capstone — Repository Explorer (v0.1)**.

## Setup

- .NET 10 SDK
- Visual Studio 2026, or the `dotnet` CLI
- An OpenAI API key

Two packages, both in `src/MiniCode.Cli`:

| Package | Version | Why |
|---|---|---|
| `Microsoft.Agents.AI` | 1.20.0 | `AIAgent`, `ChatClientAgent`, `AgentSession` |
| `Microsoft.Extensions.AI.OpenAI` | 10.9.0 | `AsIChatClient()` — adapts the OpenAI SDK to `IChatClient` |

Set the key before running:

```powershell
$env:OPENAI_API_KEY = "sk-..."
```

> **Currency note.** MAF is under active development and names have moved. `AgentSession` is obtained from `agent.CreateSessionAsync()`; the older `AgentThread` / `GetNewThread()` pairing is superseded. Non-streaming runs return `AgentResponse` — preview builds called it `AgentRunResponse` and that name is gone. Streaming still yields `AgentResponseUpdate`. If you follow an older sample and hit a missing type, this is usually why.

## Core Concepts

**Four objects, and each one has exactly one job.** Almost all of MAF's surface area comes down to these, and every later Module either wraps one or feeds one.

**`IChatClient` — the model, behind an abstraction.** This is the provider boundary. It comes from `Microsoft.Extensions.AI`, not from MAF, and it knows nothing about agents. Ours wraps the OpenAI SDK, and that is the *only* place in the whole course that OpenAI is named. Swapping providers later is a one-line change here.

**`ChatClientAgent` — the model plus a role.** An `IChatClient` answers a list of messages. An agent has an identity, standing instructions, and the ability to hold tools. `ChatClientAgent` is the adapter that turns the former into the latter, and it is the `AIAgent` MiniCode uses for the rest of the series. There is only ever one agent in this course.

**`AgentSession` — the conversation's memory.** Without it, every turn starts from nothing and the agent cannot answer "what did I just say?". The session is what makes a *conversation* rather than a sequence of unrelated questions. It matters more than it looks: Module 8 rewrites this history to keep long sessions inside the model's context window, and it can only do that because the session is a thing we hold rather than something buried inside the framework.

**Streaming — because waiting feels broken.** A coding agent may think for many seconds. `RunStreamingAsync` yields text as the model produces it, so the user sees progress immediately. The non-streaming `RunAsync` exists and returns everything at once; for an interactive tool, streaming is the right default and we use it from the start.

**Where the key comes from.** The API key is read from the environment, never hardcoded and never committed. Right now that read happens inline in `Program.cs`. Module 3 moves it behind a single configuration type so that exactly one place in the solution touches a credential — but the rule starts here.

## The Code

### `MiniCode.slnx`

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/MiniCode.Cli/MiniCode.Cli.csproj" />
  </Folder>
</Solution>
```

### `src/MiniCode.Cli/MiniCode.Cli.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>MiniCode.Cli</RootNamespace>
    <AssemblyName>MiniCode.Cli</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Agents.AI" Version="1.20.0" />
    <PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="10.9.0" />
  </ItemGroup>

</Project>
```

### `src/MiniCode.Cli/Program.cs`

```csharp
// -----------------------------------------------------------------------------
// MiniCode - a single file, on purpose
//
// An IChatClient, a ChatClientAgent wrapped around it, an AgentSession that
// remembers the conversation, and a streaming console loop. The next Module
// splits these four jobs across real projects.
// -----------------------------------------------------------------------------

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace MiniCode.Cli;

/// <summary>
/// Entry point for MiniCode. At this stage the agent is conversational only:
/// it has no filesystem access, no tools, and no shell.
/// </summary>
internal static class Program
{
    private const string ModelId = "gpt-4.1-mini";

    private static async Task Main()
    {
        string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? throw new InvalidOperationException(
                "OPENAI_API_KEY is not set. Set it before running MiniCode.");

        // Microsoft.Extensions.AI.OpenAI adapts the OpenAI SDK's ChatClient to
        // IChatClient, the provider-agnostic abstraction MAF builds on.
        ChatClient openAiClient = new OpenAIClient(apiKey).GetChatClient(ModelId);
        IChatClient chatClient = openAiClient.AsIChatClient();

        // ChatClientAgent turns any IChatClient into a MAF agent. Swapping model
        // providers later means swapping only the IChatClient passed in here.
        AIAgent agent = new ChatClientAgent(
            chatClient,
            instructions: "You are MiniCode, a concise assistant for software developers.",
            name: "MiniCode");

        // The session carries conversation history across turns.
        AgentSession session = await agent.CreateSessionAsync();

        Console.WriteLine("MiniCode. Type 'exit' to quit.");

        while (true)
        {
            Console.Write("\n> ");
            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input) ||
                input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(input, session))
            {
                Console.Write(update);
            }

            Console.WriteLine();
        }
    }
}
```

## Walkthrough

1. **The key first, and loudly.** A missing `OPENAI_API_KEY` throws immediately with a message aimed at a person. Failing at startup beats failing on the first request.
2. **Two lines to reach the model.** `GetChatClient(ModelId)` gives the OpenAI SDK's client; `AsIChatClient()` adapts it. After this line nothing downstream knows the provider.
3. **`ChatClientAgent` adds the role.** Instructions and a name are all it takes. Note it accepts tools too — that parameter is how Module 5a gives the agent hands.
4. **One session, created once, outside the loop.** Creating it per turn would be the classic bug: an agent that forgets everything between questions.
5. **`await foreach` over `RunStreamingAsync`.** Each `AgentResponseUpdate` is a fragment; `Console.Write(update)` prints its text via `ToString()`. Passing `session` is what threads this turn onto the conversation.
6. **`exit` or an empty line ends the loop**, and the banner stays fixed for the whole series — it never advertises capabilities, so it never has to be rewritten.

## Exercise

**Add a non-streaming mode.** Give the loop a second path that calls `agent.RunAsync(input, session)` instead of `RunStreamingAsync`, triggered by a command such as `!once`, and print the resulting `AgentResponse` in one go.

Acceptance criteria:

- Typing `!once` followed by a question prints the whole answer at once, with no incremental output.
- Normal input still streams.
- Both paths share the same `AgentSession`, so a `!once` answer is remembered by the next streamed turn and vice versa. Verify by asking a follow-up question that depends on the previous answer.

Doing this makes the difference between the two APIs obvious in a way reading about it does not — and it demonstrates why streaming is the default choice for an interactive tool.

## Expected Output

```text
> dotnet run --project src/MiniCode.Cli

MiniCode. Type 'exit' to quit.

> Explain dependency injection in ASP.NET Core in two sentences.

Dependency injection in ASP.NET Core is a built-in pattern where a service
container creates and supplies the objects a class needs, instead of the class
constructing them itself. You register services at startup and request them via
constructor parameters, which keeps components loosely coupled and testable.

> What did I just ask you about?

You asked for a two-sentence explanation of dependency injection in ASP.NET Core.

> exit
```

The second answer is the one that matters — it proves the `AgentSession` is carrying history. Remove the session from the `RunStreamingAsync` call and that reply becomes "I don't have any previous context."
