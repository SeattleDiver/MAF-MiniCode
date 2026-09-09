namespace MiniCode.Agent;

/// <summary>
/// A request turned into steps the operator can read before anything happens.
/// Immutable: every change returns a new plan, so a plan that was shown cannot
/// quietly become a different one.
/// </summary>
/// <param name="Goal">The request this plan is for.</param>
/// <param name="Tasks">The steps, in the order they should be attempted.</param>
public sealed record TaskPlan(string Goal, IReadOnlyList<PlanTask> Tasks)
{
    /// <summary>No plan yet.</summary>
    public static TaskPlan None { get; } = new(string.Empty, []);

    /// <summary>True when there is nothing to show.</summary>
    public bool IsEmpty => Tasks.Count == 0;

    /// <summary>The first step not yet finished, or null when the plan is done.</summary>
    public PlanTask? Current => Tasks.FirstOrDefault(t => t.State is not TaskState.Complete);

    /// <summary>True when every step is complete.</summary>
    public bool IsComplete => !IsEmpty && Tasks.All(t => t.State == TaskState.Complete);

    /// <summary>Returns a plan with one step moved to a new state.</summary>
    public TaskPlan WithState(int id, TaskState state, string? note = null) =>
        this with
        {
            Tasks = [.. Tasks.Select(t => t.Id == id ? t with { State = state, Note = note } : t)],
        };

    /// <summary>
    /// Replaces the unfinished steps with new ones, keeping everything already
    /// complete and continuing the numbering — so revising a plan never rewrites
    /// history the operator has already watched happen.
    /// </summary>
    public TaskPlan Revise(IEnumerable<string> remainingTitles)
    {
        List<PlanTask> kept = [.. Tasks.Where(t => t.State == TaskState.Complete)];
        int next = Tasks.Count == 0 ? 1 : Tasks.Max(t => t.Id) + 1;

        return this with
        {
            Tasks = [.. kept, .. remainingTitles.Select(title => new PlanTask(next++, title, TaskState.Pending))],
        };
    }

    /// <summary>Renders the plan the way the operator sees it.</summary>
    public string Render() =>
        string.Join(Environment.NewLine, Tasks.Select(t =>
            $"{t.Id}. [{t.Marker}] {t.Title}{(t.Note is null ? "" : $"  ({t.Note})")}"));
}
