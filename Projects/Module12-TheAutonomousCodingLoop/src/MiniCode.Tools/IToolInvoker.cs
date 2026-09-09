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
