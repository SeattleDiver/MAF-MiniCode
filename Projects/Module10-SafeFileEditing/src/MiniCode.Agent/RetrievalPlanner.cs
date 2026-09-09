namespace MiniCode.Agent;

/// <summary>
/// Turns search hits into a short list of files worth opening. The ranking is
/// crude on purpose — a file that matched eight times is a better guess than one
/// that matched once, and that is the whole heuristic.
/// </summary>
public static class RetrievalPlanner
{
    /// <summary>The most files one plan will propose reading.</summary>
    public const int MaxFiles = 5;

    /// <summary>
    /// Builds a plan from <c>path:line: text</c> hits as returned by search.
    /// Lines that do not carry a path are ignored rather than guessed at.
    /// </summary>
    public static ReadPlan FromSearch(IReadOnlyList<string>? hits)
    {
        if (hits is null || hits.Count == 0)
        {
            return new ReadPlan([], 0, 0);
        }

        List<IGrouping<string, string>> byFile = [.. hits
            .Select(PathOf)
            .Where(p => p.Length > 0)
            .GroupBy(p => p)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)];

        return new ReadPlan(
            [.. byFile.Take(MaxFiles).Select(g => g.Key)],
            byFile.Count,
            byFile.Sum(g => g.Count()));
    }

    private static string PathOf(string hit)
    {
        int colon = hit.IndexOf(':', StringComparison.Ordinal);
        return colon > 0 ? hit[..colon] : string.Empty;
    }
}
