# Module 2b — Microsoft Agent Framework Fundamentals: Configuring Model Providers

## Project Overview

We move the model connection out of `Program.cs` into one small type, `ChatClientFactory`, so the rest of MiniCode never names a provider. The same factory builds a client for OpenAI (the default), Azure OpenAI or Google Gemini, chosen by a single `#define` — which is what lets a demo switch provider without touching the agent.

## Prerequisites

**Starting point:** open Module02a-CreatingAnAgent/.

Module 2a's four objects — `IChatClient`, `ChatClientAgent`, `AgentSession` and streaming. Only the first one changes here; the other three are untouched.

Ships as part of **Phase 1 Capstone — Repository Explorer (v0.1)**.

## Setup

No new package. Azure OpenAI and Gemini are both reached through the `Microsoft.Extensions.AI.OpenAI` and OpenAI SDK pair Module 2a already references.

OpenAI stays the default and still needs only `OPENAI_API_KEY`. The alternates:

| Provider | Switch, in `ChatClientFactory.cs` | Environment variables |
|---|---|---|
| OpenAI | *(none)* | `OPENAI_API_KEY` |
| Azure OpenAI | `#define AZURE_OPENAI` | `AZURE_OPENAI_ENDPOINT` (e.g. `https://my-resource.openai.azure.com`), `AZURE_OPENAI_API_KEY` |
| Gemini | `#define GEMINI` | `GEMINI_API_KEY` |

> **Currency note.** Gemini's OpenAI-compatible endpoint chats with every current model, but Gemini 3.x models reject the second half of a tool call there — *"Function call is missing a thought_signature"* — because the OpenAI SDK has nowhere to carry that field. `gemini-2.5-flash` completes tool calls, which MiniCode needs from Module 5a on, so that is the pin. Azure's `/openai/v1/` endpoint accepts the plain OpenAI SDK, so the separate `Azure.AI.OpenAI` package — whose last stable release predates the OpenAI SDK this course uses — is not needed.

## Core Concepts

**One provider boundary, one file.** Module 2a said `IChatClient` is the provider boundary. Now the code agrees: everything provider-specific — the address, the key's name, the model — lives in `ChatClientFactory`. `Program.cs` asks for an `IChatClient` and never learns where it came from.

**Many providers speak OpenAI's protocol.** Azure OpenAI and Gemini both accept the OpenAI chat API at their own address. So one client class covers all three, and the only differences are the base URL, which key, and which model name.

**A compile-time switch.** `#define` picks the provider when the code is built. The default — no define at all — behaves exactly like Module 2a. C# only accepts `#define` above everything else in a file, and it applies to that file alone; that is why the factory lives by itself.

**On Azure, the "model" is a deployment.** You deploy a model in the Azure portal under a name you choose, and requests address that name. With `AZURE_OPENAI` defined, `ModelId` is that deployment name.

**Exactly one type reads the environment.** From here on, that type is the factory. Every setting a provider needs later joins it here rather than appearing anywhere else.

## The Code

`MiniCode.slnx` and `src/MiniCode.Cli/MiniCode.Cli.csproj` are unchanged from Module 2a.

### `src/MiniCode.Cli/ChatClientFactory.cs`

```csharp
// Uncomment one line to switch this build to another provider. With neither,
// MiniCode talks to OpenAI.
// #define AZURE_OPENAI
// #define GEMINI

using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace MiniCode.Cli;

/// <summary>
/// The one place that knows which chat provider MiniCode talks to, and the only
/// type that reads provider settings from the environment. OpenAI unless this
/// file defines AZURE_OPENAI or GEMINI.
/// </summary>
internal static class ChatClientFactory
{
#if AZURE_OPENAI
    private const string ModelId = "gpt-4.1-mini"; // the Azure deployment name
    private const string ApiKeyVariable = "AZURE_OPENAI_API_KEY";
    private const string EndpointVariable = "AZURE_OPENAI_ENDPOINT";
#elif GEMINI
    private const string ModelId = "gemini-2.5-flash"; // Gemini 3.x rejects tool calls over this endpoint
    private const string ApiKeyVariable = "GEMINI_API_KEY";
#else
    private const string ModelId = "gpt-4.1-mini";
    private const string ApiKeyVariable = "OPENAI_API_KEY";
#endif

    /// <summary>Creates the configured provider's chat client, throwing if a setting is missing.</summary>
    public static IChatClient Create()
    {
        // Every provider here speaks the OpenAI wire protocol; only the base URL differs.
        var options = new OpenAIClientOptions();
#if AZURE_OPENAI
        options.Endpoint = new Uri(new Uri(Read(EndpointVariable)), "/openai/v1/");
#elif GEMINI
        options.Endpoint = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/");
#endif
        return new OpenAIClient(new ApiKeyCredential(Read(ApiKeyVariable)), options)
            .GetChatClient(ModelId)
            .AsIChatClient();
    }

    private static string Read(string variable) =>
        Environment.GetEnvironmentVariable(variable)
            ?? throw new InvalidOperationException($"{variable} is not set. Set it before running MiniCode.");
}
```

### `src/MiniCode.Cli/Program.cs`

```csharp
// -----------------------------------------------------------------------------
// MiniCode - the agent, with the provider behind a factory
//
// ChatClientFactory hands back an IChatClient. The ChatClientAgent around it,
// the AgentSession that remembers the conversation, and the streaming console
// loop are the same whichever provider built it. Module 3 splits these jobs
// across real projects.
// -----------------------------------------------------------------------------

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace MiniCode.Cli;

/// <summary>
/// Entry point for MiniCode. At this stage the agent is conversational only:
/// it has no filesystem access, no tools, and no shell.
/// </summary>
internal static class Program
{
    private static async Task Main()
    {
        // ChatClientFactory decides which provider this build talks to.
        IChatClient chatClient = ChatClientFactory.Create();

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

1. **The `#define` lines come first.** Above the `using` directives is the only place C# accepts them. Uncommenting one changes how this one file compiles and nothing else.
2. **`#if` / `#elif` / `#else` picks three constants.** If both lines are uncommented, `AZURE_OPENAI` wins, because it is tested first.
3. **`OpenAIClientOptions` carries the address.** OpenAI needs none. Azure appends `/openai/v1/` to the resource address — `new Uri(base, "/openai/v1/")` replaces any path that was pasted with it. Gemini's address is fixed.
4. **`Read` fails loudly, naming the variable.** The default build's message is word for word Module 2a's.
5. **`Program.cs` got shorter.** The two OpenAI `using`s, the model constant and the key check are gone; one line remains.

## Exercise

**Add a fourth provider: a local model served by Ollama.** Ollama exposes an OpenAI-compatible endpoint at `http://localhost:11434/v1/` and does not check the key. The work belongs in `src/MiniCode.Cli/ChatClientFactory.cs`.

Acceptance criteria:

- A new `#define OLLAMA` selects it, and with no define the build still talks to OpenAI.
- With Ollama running and a model pulled (for example `ollama pull llama3.2`), the two-turn conversation below works — the second answer remembers the first.
- Ollama needs no environment variable. The factory still passes a non-empty key string, because the OpenAI SDK refuses an empty one.
- `Program.cs` does not change.

This goes beyond what MiniCode needs, so the criteria are the specification; no reference implementation follows.

## Expected Output

The default build talks to OpenAI, exactly as in Module 2a:

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

Uncomment `#define GEMINI`, set `GEMINI_API_KEY`, and run again — same prompt, same `Program.cs`, a different model answering:

```text
MiniCode. Type 'exit' to quit.

> Explain dependency injection in ASP.NET Core in two sentences.

Dependency Injection (DI) is a design pattern where objects receive their
dependencies from an external source rather than creating them, promoting loose
coupling and testability. In ASP.NET Core, it's natively supported through a
built-in Inversion of Control (IoC) container, allowing services to be
registered in `Program.cs` and then automatically resolved and injected into
class constructors.

> What did I just ask you about?

You asked me about **Dependency Injection in ASP.NET Core**.

> exit
```

Forget the key for whichever provider is selected, and the factory says which one it wanted:

```text
Unhandled exception. System.InvalidOperationException: OPENAI_API_KEY is not set. Set it before running MiniCode.
```
