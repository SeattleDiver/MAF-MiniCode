# Module 5a — Giving the Agent Read Access: The Interception Seam

## Project Overview

We give the agent its first tool — and, more importantly, we decide *how* every tool call will be dispatched for the rest of the course. Modules 15 and 17 both need to wrap every call the model makes; getting that seam right now is the difference between adding a decorator later and reopening every tool file.

## Prerequisites

**Starting point:** open Module04-BuildingTheWorkspaceSandbox/.

Module 3's project layout and `ICodingAgent`, and Module 4's `IWorkspace`. Module 4's first exercise — wiring the workspace root into startup — is delivered here, because the first tool is what finally needs it.

Ships as part of **Phase 1 Capstone — Repository Explorer (v0.1)**.

## Setup

One new package, in `MiniCode.Tools`:

| Package | Version | Why |
|---|---|---|
| `Microsoft.Extensions.AI` | 10.9.0 | `AIFunctionFactory`, `AIFunction`, `AITool`, `DelegatingAIFunction` |

Note it goes in `MiniCode.Tools`, not `MiniCode.Agent`. Tool *definitions* are the Tools project's job.

> **Currency note.** Tools are built with `AIFunctionFactory.Create`, which reads the method signature and its `description` to produce the JSON schema the model sees. `AIFunction.InvokeAsync` is **not** virtual — the extension point is `protected virtual InvokeCoreAsync`, and `Microsoft.Extensions.AI` ships a concrete `DelegatingAIFunction` to derive from. Older samples hand-roll an `AIFunction` subclass; that is no longer necessary. Also note `ChatClientAgent` wires up function invocation itself — you do **not** need `UseFunctionInvocation()` on the `IChatClient`.

## Core Concepts

**A tool is a C# method the model is allowed to call.** You write an ordinary method; `AIFunctionFactory.Create` turns it into an `AIFunction` by reading its parameters and the description you supply. The description is not documentation — it is the model's only instruction on when and how to call the thing, and a vague one produces a tool the model misuses.

**The model never calls your code directly.** It emits a request to call a named function. The framework matches the name, binds the arguments, invokes the function, and feeds the return value back into the conversation as a result the model then reasons about.

**Two Modules from now, we will need to intervene in every call.** Module 15 puts a human in front of dangerous operations. Module 17 records what happened. Both need to sit between "the model asked" and "the code ran" — for *every* tool, including ones not written yet.

**The naive approach fails, and it fails silently.** If each tool asks for approval itself, then approval is only as good as the least careful tool author, and adding logging means editing every file again. The alternative — one chokepoint — is worth arranging before there are tools to retrofit.

**But you cannot put a chokepoint just anywhere, because MAF does not call us.** It calls the function object it was handed at agent-construction time. A gate placed anywhere else is a gate the framework routes around. So the interception has to live *in the function object itself*.

**`DelegatingAIFunction` is exactly that hook.** It forwards the name, description and JSON schema to the function it wraps — so the model sees an identical tool — and leaves one method for us to override. Wrap every tool as it is created, and there is no path that yields an unwrapped one.

**The seam's contract: an interceptor must never throw.** Its return value goes into the model's context. With the framework's defaults, an escaping exception reaches the model as the bare string `Error: Function failed.` and a few in a row end the run — so the agent loses its turn and learns nothing. A refusal has to be a sentence the model can act on. The one exception is cancellation: an abandoned run is not a tool failure, so that propagates.

**Today the chain has one link.** `DirectToolInvoker` runs the tool. That is deliberately boring — the point of this Module is that Module 15 becomes `new ApprovalToolInvoker(new DirectToolInvoker(), …)` and nothing else changes.

## The Code

### `src/MiniCode.Tools/ToolInvocation.cs`

```csharp
using Microsoft.Extensions.AI;

namespace MiniCode.Tools;

/// <summary>
/// One about-to-happen tool call, as data. This is the whole vocabulary an
/// interceptor needs: it can allow the call, time it, or answer it itself,
/// without knowing which tool it is looking at.
/// </summary>
/// <param name="Function">The undecorated function. Invoking this runs the tool for real.</param>
/// <param name="Arguments">The arguments the model supplied, already bound by name.</param>
public sealed record ToolInvocation(AIFunction Function, AIFunctionArguments Arguments)
{
    /// <summary>The tool's name, as the model knows it.</summary>
    public string Name => Function.Name;
}
```

### `src/MiniCode.Tools/IToolInvoker.cs`

```csharp
namespace MiniCode.Tools;

/// <summary>
/// The single point every tool call passes through. Module 15 adds approval and
/// Module 17 adds tracing by wrapping this, not by editing any tool.
/// </summary>
public interface IToolInvoker
{
    /// <summary>
    /// Runs, refuses, or observes a call. Implementations must not throw: the
    /// return value lands in the model's context, so a refusal has to be text.
    /// </summary>
    ValueTask<object?> InvokeAsync(ToolInvocation invocation, CancellationToken cancellationToken);
}
```

### `src/MiniCode.Tools/DirectToolInvoker.cs`

```csharp
namespace MiniCode.Tools;

/// <summary>
/// The innermost invoker: it actually runs the tool. Every chain ends here, and
/// this is where the never-throw rule is enforced rather than merely stated.
/// </summary>
public sealed class DirectToolInvoker : IToolInvoker
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        ToolInvocation invocation,
        CancellationToken cancellationToken)
    {
        try
        {
            return await invocation.Function.InvokeAsync(invocation.Arguments, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // An abandoned run is not a tool failure; let it propagate.
            throw;
        }
        catch (Exception ex)
        {
            return $"{invocation.Name} failed: {ex.Message}";
        }
    }
}
```

### `src/MiniCode.Tools/InterceptedFunction.cs`

```csharp
using Microsoft.Extensions.AI;

namespace MiniCode.Tools;

/// <summary>
/// Joins the <see cref="IToolInvoker"/> seam to the way MAF actually dispatches
/// tools. <see cref="DelegatingAIFunction"/> forwards the name, description and
/// JSON schema, so the model sees an identical tool, and leaves
/// <see cref="InvokeCoreAsync"/> for us to override. That override is the whole
/// mechanism — there is no approval, logging or timing in this file.
/// </summary>
public sealed class InterceptedFunction(AIFunction innerFunction, IToolInvoker invoker)
    : DelegatingAIFunction(innerFunction)
{
    /// <summary>
    /// Hands the call to the invoker. The invocation carries
    /// <see cref="DelegatingAIFunction.InnerFunction"/>, not this wrapper, so the
    /// innermost invoker runs the tool once instead of re-entering the chain.
    /// </summary>
    protected override ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken) =>
        invoker.InvokeAsync(new ToolInvocation(InnerFunction, arguments), cancellationToken);
}
```

### `src/MiniCode.Tools/ToolCatalog.cs`

```csharp
using Microsoft.Extensions.AI;
using MiniCode.Workspace;

namespace MiniCode.Tools;

/// <summary>
/// Builds the tools the model may call. Every tool leaves here already wrapped
/// in an <see cref="InterceptedFunction"/>, so there is no code path that
/// produces an unwrapped one.
/// </summary>
public sealed class ToolCatalog(IWorkspace workspace, IToolInvoker invoker)
{
    /// <summary>The tools to hand to the agent.</summary>
    public IReadOnlyList<AITool> GetTools() =>
    [
        Intercept(AIFunctionFactory.Create(
            WorkspaceRoot,
            name: "workspace_root",
            description: "Returns the absolute path of the repository MiniCode is working in. "
                       + "Every other tool takes paths relative to this root.")),
    ];

    private AITool Intercept(AIFunction function) => new InterceptedFunction(function, invoker);

    private string WorkspaceRoot() => workspace.Root;
}
```

The composition root changes to build these and pass them to the agent. From `src/MiniCode.Agent/CodingAgentFactory.cs`:

```csharp
var workspace = new MiniCode.Workspace.Workspace(workspaceRoot);
var catalog = new ToolCatalog(workspace, new DirectToolInvoker());

AIAgent agent = new ChatClientAgent(
    chatClient,
    Instructions,
    name: "MiniCode",
    tools: [.. catalog.GetTools()]);
```

## Walkthrough

1. **`ToolInvocation` is deliberately thin** — a function and its arguments. An interceptor that needed to know *which* tool it was gating would be a interceptor you have to update for every new tool.
2. **`Function` is the inner, undecorated function.** This is the detail that prevents infinite recursion: pass the wrapper and the innermost invoker would re-enter the chain forever.
3. **`DirectToolInvoker` turns a thrown exception into a sentence.** The tool's own bug becomes `read_file failed: Access to the path is denied.` — text the model can react to, rather than a dead turn.
4. **Cancellation is re-thrown, not swallowed.** Module 16 needs to tell "the user pressed Ctrl+C" apart from "the tool broke", and that distinction is made here.
5. **`InterceptedFunction` overrides exactly one method.** Everything the model sees — name, description, schema — comes from the base class forwarding to the inner function.
6. **`ToolCatalog.Intercept` is private**, and every entry in `GetTools()` goes through it. That is what makes "no tool bypasses the seam" a property of the code rather than a convention.
7. **`workspace_root` is a real tool with one job.** It exists so you can watch a tool call happen end to end — and because its description is where the model is told that every other path is relative.

## Exercise

**Write a second invoker and compose it.** Create `src/MiniCode.Tools/CountingToolInvoker.cs` that wraps another `IToolInvoker`, counts the calls it sees, and passes each one through unchanged. Then in `CodingAgentFactory`, build the catalog with `new CountingToolInvoker(new DirectToolInvoker())` and print the count when the session ends.

Acceptance criteria:

- The model's behaviour is identical — the wrapper changes nothing the model can observe.
- Asking MiniCode where it is working increments the count by exactly one.
- The counting invoker does not throw, even if the inner one returns a failure sentence.
- No file in `MiniCode.Tools` other than your new one and `CodingAgentFactory` changes.

That last criterion is the real lesson: you just added cross-cutting behaviour to every tool in the system without opening a single tool. Module 15 does the same thing with an approval prompt, and Module 17 with tracing.

## Expected Output

```text
> dotnet run --project src/MiniCode.Cli -- D:\Repos\CustomerPortal

Workspace: D:\Repos\CustomerPortal
MiniCode. Type 'exit' to quit.

> Which repository are you working in?

I'm working in D:\Repos\CustomerPortal.

> exit
```

Short, and the interesting part is invisible: answering that question required the model to decide it needed a tool, emit a call to `workspace_root`, and read the result back. The call travelled through `InterceptedFunction` into `DirectToolInvoker` — the same path that will carry an approval prompt in Module 15 and a trace line in Module 17.
