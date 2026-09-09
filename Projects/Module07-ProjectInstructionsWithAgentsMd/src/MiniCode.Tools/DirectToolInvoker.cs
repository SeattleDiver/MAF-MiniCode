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
