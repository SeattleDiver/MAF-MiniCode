namespace MiniCode.Agent;

/// <summary>
/// An approximation of how many tokens a piece of text costs. It is deliberately
/// arithmetic rather than a real tokenizer: MiniCode needs a number good enough
/// to make a decision with, not one good enough to bill on.
/// </summary>
public static class TokenEstimator
{
    /// <summary>Roughly the characters-per-token ratio of English and C# together.</summary>
    public const int CharactersPerToken = 4;

    /// <summary>What each message costs in framing, on top of its text.</summary>
    public const int MessageOverhead = 4;

    /// <summary>Estimates one message. Never negative, never exact.</summary>
    public static int Estimate(string? text) =>
        string.IsNullOrEmpty(text) ? MessageOverhead : (text.Length / CharactersPerToken) + MessageOverhead;
}
