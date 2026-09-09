using MiniCode.Agent;
using Xunit;

namespace MiniCode.Tests;

/// <summary>Verifies plan parsing, state and revision. Ours, not the course's.</summary>
public sealed class PlanTests
{
    private static TaskPlan Sample() => Planner.Parse("goal", """
        Here is a plan:
        1. Inspect CustomerService
        2. Identify existing caching infrastructure
        3. Determine an appropriate cache lifetime
        Let me know if you want it narrower.
        """);

    [Fact]
    public void ParsesNumberedLinesAndIgnoresProse()
    {
        TaskPlan plan = Sample();
        Assert.Equal(3, plan.Tasks.Count);
        Assert.Equal("Inspect CustomerService", plan.Tasks[0].Title);
        Assert.DoesNotContain(plan.Tasks, t => t.Title.Contains("narrower", StringComparison.Ordinal));
    }

    [Fact]
    public void AReplyWithNoStepsIsNoPlan()
    {
        Assert.True(Planner.Parse("goal", "I need more information.").IsEmpty);
        Assert.True(Planner.Parse("goal", null).IsEmpty);
    }

    [Fact]
    public void CurrentSkipsCompletedStepsButStopsOnBlocked()
    {
        TaskPlan plan = Sample().WithState(1, TaskState.Complete).WithState(2, TaskState.Blocked, "no cache layer");
        Assert.Equal(2, plan.Current!.Id);
        Assert.Equal("no cache layer", plan.Current.Note);
    }

    [Fact]
    public void ReviseKeepsCompletedStepsAndNeverReusesIds()
    {
        TaskPlan plan = Sample().WithState(1, TaskState.Complete).Revise(["Add IMemoryCache", "Build"]);

        Assert.Equal([1, 4, 5], plan.Tasks.Select(t => t.Id));
        Assert.Equal(TaskState.Complete, plan.Tasks[0].State);
    }

    [Fact]
    public void PlansAreImmutable()
    {
        TaskPlan original = Sample();
        original.WithState(1, TaskState.Complete);
        Assert.Equal(TaskState.Pending, original.Tasks[0].State);
    }

    [Fact]
    public void RenderMarksEachState()
    {
        string rendered = Sample().WithState(1, TaskState.Complete).WithState(2, TaskState.InProgress).Render();
        Assert.Contains("1. [x]", rendered);
        Assert.Contains("2. [>]", rendered);
        Assert.Contains("3. [ ]", rendered);
    }

    [Fact]
    public void IsCompleteOnlyWhenEveryStepIs()
    {
        TaskPlan plan = Sample();
        Assert.False(plan.IsComplete);
        Assert.True(plan.WithState(1, TaskState.Complete)
                        .WithState(2, TaskState.Complete)
                        .WithState(3, TaskState.Complete).IsComplete);
    }
}
