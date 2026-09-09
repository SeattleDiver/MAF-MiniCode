namespace MiniCode.Agent;

/// <summary>
/// How much room the model has, and when MiniCode should start worrying. The
/// window is finite and shared by the instructions, the conversation and every
/// tool result — spending it on a file nobody needed is the cost this exists
/// to make visible.
/// </summary>
/// <param name="MaxTokens">The model's context window.</param>
/// <param name="CompactAtFraction">The fraction at which compaction becomes due.</param>
public sealed record ContextBudget(int MaxTokens, double CompactAtFraction)
{
    /// <summary>The window MiniCode assumes for gpt-4.1-mini.</summary>
    public static ContextBudget Default { get; } = new(128_000, 0.75);

    /// <summary>True once the session is close enough to full to need attention.</summary>
    public bool ShouldCompact(int usedTokens) => usedTokens >= MaxTokens * CompactAtFraction;

    /// <summary>A one-line report, with a tilde because the number is an estimate.</summary>
    public string Describe(int usedTokens) =>
        $"context ~{usedTokens:N0} / {MaxTokens:N0} tokens ({(double)usedTokens / MaxTokens:P0})";
}
