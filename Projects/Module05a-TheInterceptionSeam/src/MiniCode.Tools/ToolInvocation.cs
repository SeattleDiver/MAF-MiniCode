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
