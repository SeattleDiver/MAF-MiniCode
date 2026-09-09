using System.Text.Json;
using MiniCode.Tools;

namespace MiniCode.Agent;

/// <summary>
/// Wraps Module 5a's <see cref="IToolInvoker"/> seam and pauses in front of the
/// calls the syllabus marks as needing a human: writing to the repository, and
/// any shell command other than a build or test run. Once the operator answers
/// "always", nothing is asked again for the rest of the session.
/// </summary>
public sealed class ApprovalInvoker(IToolInvoker inner, IApprovalPrompter prompter) : IToolInvoker
{
    private bool _alwaysApprove;

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(ToolInvocation invocation, CancellationToken cancellationToken)
    {
        if (_alwaysApprove || !RequiresApproval(invocation))
        {
            return await inner.InvokeAsync(invocation, cancellationToken);
        }

        ApprovalDecision decision = await prompter.AskAsync(Describe(invocation), cancellationToken);
        _alwaysApprove = decision == ApprovalDecision.AlwaysApprove;

        return decision == ApprovalDecision.Deny
            ? $"{invocation.Name} was not approved and did not run."
            : await inner.InvokeAsync(invocation, cancellationToken);
    }

    private static bool RequiresApproval(ToolInvocation invocation) => invocation.Name switch
    {
        "write_file" or "edit_file" => true,
        "run_command" => !IsBuildOrTest(invocation),
        _ => false,
    };

    // A pending call's arguments can already be a JsonElement rather than a CLR
    // string[] — the same shape the Phase 3 Capstone's WorkingState note had to
    // account for. Serializing through JsonElement normalizes both.
    private static bool IsBuildOrTest(ToolInvocation invocation)
    {
        JsonElement arguments = JsonSerializer.SerializeToElement(invocation.Arguments);
        return arguments.TryGetProperty("command", out JsonElement command)
            && string.Equals(command.GetString(), "dotnet", StringComparison.OrdinalIgnoreCase)
            && arguments.TryGetProperty("arguments", out JsonElement args)
            && args.ValueKind == JsonValueKind.Array
            && args.GetArrayLength() > 0
            && args[0].GetString() is "build" or "test";
    }

    private static string Describe(ToolInvocation invocation) =>
        $"{invocation.Name}({JsonSerializer.Serialize(invocation.Arguments)})";
}
