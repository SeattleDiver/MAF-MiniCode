using System.Text.Json;
using MiniCode.Tools;

namespace MiniCode.Agent;

/// <summary>
/// Stops the model from spinning: a hard cap on tool calls for one request,
/// and a refusal for a call repeated with identical arguments. Wraps
/// <see cref="IToolInvoker"/> exactly the way Module 11b's
/// AllowListedShellExecutor wraps IShellExecutor — every call passes through
/// here, whether or not this guard has anything to say about it.
/// </summary>
public sealed class LoopGuardInvoker(IToolInvoker inner) : IToolInvoker
{
    /// <summary>The most tool calls one request may make before it is cut off.</summary>
    public const int MaxCallsPerRequest = 20;

    private int _calls;
    private string? _lastCall;

    /// <summary>Clears state. Call once per user request, before the model starts.</summary>
    public void Reset()
    {
        _calls = 0;
        _lastCall = null;
    }

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(ToolInvocation invocation, CancellationToken cancellationToken)
    {
        if (++_calls > MaxCallsPerRequest)
        {
            return $"Stopped after {MaxCallsPerRequest} tool calls in this request. "
                 + "Summarize what you found or changed instead of continuing.";
        }

        string call = invocation.Name + JsonSerializer.Serialize(invocation.Arguments);
        if (call == _lastCall)
        {
            return $"'{invocation.Name}' was just called with these exact arguments. Repeating it will not "
                 + "produce new information — try a different command, argument, or file.";
        }

        _lastCall = call;
        return await inner.InvokeAsync(invocation, cancellationToken);
    }
}
