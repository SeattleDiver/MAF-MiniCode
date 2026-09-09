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
