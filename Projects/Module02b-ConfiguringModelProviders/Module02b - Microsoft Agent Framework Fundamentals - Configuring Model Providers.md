# Module 2b — Microsoft Agent Framework Fundamentals: Configuring Model Providers

## Project Overview

We move the model connection out of `Program.cs` into one small type, `ChatClientFactory`, so the rest of MiniCode never names a provider. The same factory builds a client for OpenAI (the default), Azure OpenAI, Google Gemini or a local model served by Ollama, chosen by one environment variable, `MINI_CODE_LLM` — which is what lets a demo switch provider without touching the agent or rebuilding.

## Prerequisites

**Starting point:** open Module02a-CreatingAnAgent/.

Module 2a's four objects — `IChatClient`, `ChatClientAgent`, `AgentSession` and streaming. Only the first one changes here; the other three are untouched.

Ships as part of **Phase 1 Capstone — Repository Explorer (v0.1)**.

## Setup

No new package. Azure OpenAI, Gemini and Ollama are all reached through the `Microsoft.Extensions.AI.OpenAI` and OpenAI SDK pair Module 2a already references.

One new, optional environment variable: `MINI_CODE_LLM`. Leave it unset and OpenAI stays the default, needing only `OPENAI_API_KEY`. The alternates:

| Provider | `MINI_CODE_LLM` | Environment variables |
|---|---|---|
| OpenAI | *(unset)* or `OPENAI` | `OPENAI_API_KEY` |
| Azure OpenAI | `AZURE` | `AZURE_OPENAI_ENDPOINT` (e.g. `https://my-resource.openai.azure.com`), `AZURE_OPENAI_API_KEY` |
| Gemini | `GEMINI` | `GEMINI_API_KEY` |
| Ollama (local) | `OLLAMA` | none — install Ollama, then `ollama pull qwen2.5-coder:7b` |

> **Currency note.** Gemini's OpenAI-compatible endpoint chats with every current model, but Gemini 3.x models reject the second half of a tool call there — *"Function call is missing a thought_signature"* — because the OpenAI SDK has nowhere to carry that field. `gemini-2.5-flash` completes tool calls, which MiniCode needs from Module 5a on, so that is the pin. Azure's `/openai/v1/` endpoint accepts the plain OpenAI SDK, so the separate `Azure.AI.OpenAI` package — whose last stable release predates the OpenAI SDK this course uses — is not needed.

> **Local models.** Ollama serves an OpenAI-compatible endpoint at `http://localhost:11434/v1/` and ignores the API key, so `OLLAMA` reads no key variable at all. `qwen2.5-coder:7b` is a 7B coding model that runs on your own machine — free and offline, but less capable than `gpt-4.1-mini`, so demos stay on OpenAI.

## Core Concepts

**One provider boundary, one file.** Module 2a said `IChatClient` is the provider boundary. Now the code agrees: everything provider-specific — the address, the key's name, the model — lives in `ChatClientFactory`. `Program.cs` asks for an `IChatClient` and never learns where it came from.

**Many providers speak OpenAI's protocol.** Azure OpenAI, Gemini and a local Ollama server all accept the OpenAI chat API at their own address. So one client class covers all four, and the only differences are the base URL, which key, and which model name. `OpenAIClient` is a client for that protocol, not for one company's servers: point its `Endpoint` at Google, Azure or `localhost`, and it sends the same requests there.

**A run-time switch.** `MINI_CODE_LLM` picks the provider each time MiniCode starts, so one build can talk to any of the four. The default — the variable not set at all — behaves exactly like Module 2a. A value the factory does not recognise fails loudly, naming the variable, rather than quietly falling back to OpenAI.

**Gather first, validate once.** Every setting starts as `null`, and each provider's `case` fills in the whole set. Only after the `switch` does the factory check for gaps and throw — so every failure is reported in one place, before any client is built.

**A local model needs no key.** Ollama runs on your own machine, so there is nothing to authenticate. The factory still hands the SDK a placeholder string, because the SDK refuses an empty one.

**On Azure, the "model" is a deployment.** You deploy a model in the Azure portal under a name you choose, and requests address that name. With `MINI_CODE_LLM=AZURE`, `modelId` is that deployment name.

**Exactly one type reads the environment.** From here on, that type is the factory. Every setting a provider needs later joins it here rather than appearing anywhere else.

## The Code

`MiniCode.slnx` and `src/MiniCode.Cli/MiniCode.Cli.csproj` are unchanged from Module 2a.

### `src/MiniCode.Cli/ChatClientFactory.cs`

```csharp
using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace MiniCode.Cli;

/// <summary>
/// The one place that knows which chat provider MiniCode talks to, and the only
/// type that reads provider settings from the environment. MINI_CODE_LLM picks
/// the provider: OPENAI (the default when it is not set), AZURE, GEMINI or OLLAMA.
/// </summary>
internal static class ChatClientFactory
{
    private const string ProviderVariable = "MINI_CODE_LLM";

    /// <summary>Creates the configured provider's chat client, throwing if a setting is missing.</summary>
    public static IChatClient Create()
    {
        string provider = Environment.GetEnvironmentVariable(ProviderVariable) ?? "OPENAI";

        // Each case assigns the whole set. Anything still null after the switch
        // is a setting that could not be found.
        Uri? endpoint = null;
        string? modelId = null;
        string? apiKeyVariable = null;
        string? apiKey = null;

        // Every provider here speaks the OpenAI wire protocol; only these values differ.
        switch (provider.ToUpperInvariant())
        {
            case "OPENAI":
                modelId = "gpt-4.1-mini";
                endpoint = new Uri("https://api.openai.com/v1/");
                apiKeyVariable = "OPENAI_API_KEY";
                apiKey = Environment.GetEnvironmentVariable(apiKeyVariable);
                break;
            case "AZURE":
                modelId = "gpt-4.1-mini"; // the Azure deployment name
                endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") is string resource
                    ? new Uri(new Uri(resource), "/openai/v1/")
                    : null;
                apiKeyVariable = "AZURE_OPENAI_API_KEY";
                apiKey = Environment.GetEnvironmentVariable(apiKeyVariable);
                break;
            case "GEMINI":
                modelId = "gemini-2.5-flash"; // Gemini 3.x rejects tool calls over this endpoint
                endpoint = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/");
                apiKeyVariable = "GEMINI_API_KEY";
                apiKey = Environment.GetEnvironmentVariable(apiKeyVariable);
                break;
            case "OLLAMA":
                modelId = "qwen2.5-coder:7b";
                endpoint = new Uri("http://localhost:11434/v1/");
                apiKeyVariable = null;
                apiKey = "ollama"; // Ollama ignores the key, but the SDK refuses an empty one
                break;
        }

        // An unrecognised provider matched no case, so even modelId is still null.
        if (modelId is null)
        {
            throw new InvalidOperationException(
                $"{ProviderVariable} is '{provider}'. Set it to OPENAI, AZURE, GEMINI or OLLAMA.");
        }

        // Only Azure reads its endpoint from the environment.
        if (endpoint is null)
        {
            throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set. Set it before running MiniCode.");
        }

        if (apiKey is null)
        {
            throw new InvalidOperationException($"{apiKeyVariable} is not set. Set it before running MiniCode.");
        }

        return new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = endpoint })
            .GetChatClient(modelId)
            .AsIChatClient();
    }
}
```

`src/MiniCode.Cli/Program.cs` loses the two OpenAI `using`s, the model constant and the key check. Inside `Main`, where Module 2a built the OpenAI client, one call remains; the file's header comment is updated to match, and nothing else changes:

```csharp
        // ChatClientFactory decides which provider this build talks to.
        IChatClient chatClient = ChatClientFactory.Create();
```

## Walkthrough

1. **`?? "OPENAI"` is the default.** An unset `MINI_CODE_LLM` reads as null, so MiniCode talks to OpenAI with no setup beyond Module 2a's key.
2. **Four settings, all `null`.** `endpoint`, `modelId`, `apiKeyVariable` and `apiKey` are declared before the `switch`, so each one has a single meaning afterwards: set, or missing.
3. **Each `case` assigns the whole set.** Reading a case top to bottom tells you everything about that provider. The value is upper-cased first, so `gemini` works too. Keys are read with `GetEnvironmentVariable`, which returns `null` rather than throwing when a variable is missing.
4. **Azure builds its address from the environment.** `new Uri(base, "/openai/v1/")` appends the path and replaces any path pasted with the resource address. If `AZURE_OPENAI_ENDPOINT` is unset, `endpoint` stays `null`. The other three addresses are fixed.
5. **Ollama sets a placeholder key.** It ignores the key, but the SDK refuses an empty one, so `apiKey` is `"ollama"` and `apiKeyVariable` is `null`.
6. **The checks run after the `switch`.** There is no `default`: an unrecognised provider matches no case and leaves `modelId` `null`, which is the first check. Then a missing Azure endpoint, then a missing key, named by `apiKeyVariable`. With OpenAI selected, the message is word for word Module 2a's.
7. **One `return` builds the client.** `OpenAIClientOptions { Endpoint = endpoint }` carries the address, whichever provider supplied it.

## Exercise

**Let the Ollama address come from the environment.** Ollama's own tools read `OLLAMA_HOST` to find a server on another port or another machine — a GPU box on your network, say. The work belongs in `src/MiniCode.Cli/ChatClientFactory.cs`.

Acceptance criteria:

- With `MINI_CODE_LLM=OLLAMA` and `OLLAMA_HOST=http://192.168.1.20:11434`, requests go to `http://192.168.1.20:11434/v1/`.
- With `OLLAMA_HOST` unset, the address is still `http://localhost:11434/v1/`.
- No other provider reads `OLLAMA_HOST`, and with `MINI_CODE_LLM` unset MiniCode still talks to OpenAI.
- `ChatClientFactory` stays the only type that reads the environment, and `Program.cs` does not change.

This goes beyond what MiniCode needs, so the criteria are the specification; no reference implementation follows.

## Expected Output

With `MINI_CODE_LLM` unset, MiniCode talks to OpenAI, exactly as in Module 2a:

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

Set `MINI_CODE_LLM=GEMINI` and `GEMINI_API_KEY`, and run again — same build, same prompt, a different model answering:

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

Set `MINI_CODE_LLM=OLLAMA` with Ollama running — no key needed — and a local model answers:

```text
MiniCode. Type 'exit' to quit.

> Explain dependency injection in ASP.NET Core in two sentences.

Dependency injection in ASP.NET Core is a design pattern where the framework
provides an interface for a service and the developer defines the actual
implementation. This allows for loose coupling and easier testing of components
by injecting mock or stub implementations.

> What did I just ask you about?

You asked me to explain dependency injection in ASP.NET Core.

> exit
```

Forget the key for whichever provider is selected, and the factory says which one it wanted:

```text
Unhandled exception. System.InvalidOperationException: OPENAI_API_KEY is not set. Set it before running MiniCode.
```
