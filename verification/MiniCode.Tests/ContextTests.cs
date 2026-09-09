using MiniCode.Agent;
using Xunit;

namespace MiniCode.Tests;

/// <summary>Verifies budget arithmetic and retrieval planning. Ours, not the course's.</summary>
public sealed class ContextTests
{
    [Fact]
    public void AnEmptyMessageStillCostsFraming() =>
        Assert.Equal(TokenEstimator.MessageOverhead, TokenEstimator.Estimate(""));

    [Fact]
    public void EstimateGrowsWithLength() =>
        Assert.True(TokenEstimator.Estimate(new string('x', 400)) > TokenEstimator.Estimate(new string('x', 40)));

    [Fact]
    public void CompactsAtThreeQuartersOfTheWindow()
    {
        ContextBudget b = ContextBudget.Default;
        Assert.False(b.ShouldCompact(95_999));
        Assert.True(b.ShouldCompact(96_000));
    }

    [Fact]
    public void DescribeMarksTheNumberAsAnEstimate() =>
        Assert.Contains("~", ContextBudget.Default.Describe(1_000));

    [Fact]
    public void AnEmptySearchPlansNothing()
    {
        ReadPlan plan = RetrievalPlanner.FromSearch([]);
        Assert.Empty(plan.Files);
        Assert.False(plan.IsNarrowed);
    }

    [Fact]
    public void RanksByMatchCountThenPath()
    {
        ReadPlan plan = RetrievalPlanner.FromSearch(
        [
            "b.cs:1: x", "a.cs:1: x", "a.cs:2: x", "a.cs:3: x", "c.cs:9: x",
        ]);

        Assert.Equal("a.cs", plan.Files[0]);
        Assert.Equal(["a.cs", "b.cs", "c.cs"], plan.Files);
        Assert.Equal(3, plan.CandidateCount);
        Assert.Equal(5, plan.MatchCount);
    }

    [Fact]
    public void CapsTheFileListAndSaysItNarrowed()
    {
        ReadPlan plan = RetrievalPlanner.FromSearch(
            [.. Enumerable.Range(0, RetrievalPlanner.MaxFiles + 4).Select(i => $"f{i}.cs:1: hit")]);

        Assert.Equal(RetrievalPlanner.MaxFiles, plan.Files.Count);
        Assert.True(plan.IsNarrowed);
    }

    [Fact]
    public void IgnoresHitsWithNoPath()
    {
        ReadPlan plan = RetrievalPlanner.FromSearch(["no-colon-here", "a.cs:1: x"]);
        Assert.Equal(["a.cs"], plan.Files);
    }
}
