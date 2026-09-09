using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MiniCode.Tools;

namespace MiniCode.Agent;

/// <summary>
/// Wraps Module 5a's <see cref="IToolInvoker"/> seam and logs every call that
/// actually reaches a tool — its name, its outcome, and how long it took.
/// Sits inside <see cref="ApprovalInvoker"/>, so a human's time spent deciding
/// is never counted as the tool's own duration.
/// </summary>
public sealed class TracingInvoker(IToolInvoker inner, ILoggerFactory loggerFactory) : IToolInvoker
{
    private readonly ILogger _toolLog = loggerFactory.CreateLogger("Tool");
    private readonly ILogger _resultLog = loggerFactory.CreateLogger("Result");

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(ToolInvocation invocation, CancellationToken cancellationToken)
    {
        _toolLog.LogInformation("{ToolName}", invocation.Name);

        var stopwatch = Stopwatch.StartNew();
        object? result = await inner.InvokeAsync(invocation, cancellationToken);
        stopwatch.Stop();

        _resultLog.LogInformation("{Summary} ({ElapsedMilliseconds}ms)", FirstLine(result), stopwatch.ElapsedMilliseconds);
        return result;
    }

    // A tool's own text already says what happened — "Exit code 0", "Wrote
    // src/A.cs (12 lines)", "run_command failed: ..." — the first line is
    // enough for a trace without repeating a whole build's output twice.
    private static string FirstLine(object? result) =>
        (result?.ToString() ?? string.Empty).Split(Environment.NewLine, 2)[0];
}
