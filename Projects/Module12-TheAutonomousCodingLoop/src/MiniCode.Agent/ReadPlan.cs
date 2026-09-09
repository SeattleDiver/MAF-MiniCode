namespace MiniCode.Agent;

/// <summary>
/// What to read after a search, and what it cost to find out. <c>Files</c> is
/// ranked most-matched first and capped, so a search that hit forty files still
/// produces a short list.
/// </summary>
/// <param name="Files">The files worth opening, best first.</param>
/// <param name="CandidateCount">How many distinct files matched at all.</param>
/// <param name="MatchCount">How many individual lines matched.</param>
public sealed record ReadPlan(IReadOnlyList<string> Files, int CandidateCount, int MatchCount)
{
    /// <summary>True when the search found more files than the plan will open.</summary>
    public bool IsNarrowed => CandidateCount > Files.Count;
}
