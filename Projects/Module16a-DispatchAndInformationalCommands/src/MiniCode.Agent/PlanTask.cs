namespace MiniCode.Agent;

/// <summary>
/// One step of a plan. <c>Id</c> is an identity, not a position: it is assigned
/// once and never reused, so a revised plan can drop or insert steps without
/// renumbering the ones already finished.
/// </summary>
/// <param name="Id">Stable identity for this step.</param>
/// <param name="Title">What the step does, in one line.</param>
/// <param name="State">How far it has got.</param>
/// <param name="Note">Why it is blocked, when it is.</param>
public sealed record PlanTask(int Id, string Title, TaskState State, string? Note = null)
{
    /// <summary>The marker shown against the step when the plan is rendered.</summary>
    public string Marker => State switch
    {
        TaskState.Complete => "x",
        TaskState.InProgress => ">",
        TaskState.Blocked => "!",
        _ => " ",
    };
}
