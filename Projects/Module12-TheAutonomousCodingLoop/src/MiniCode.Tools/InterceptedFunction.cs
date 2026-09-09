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
