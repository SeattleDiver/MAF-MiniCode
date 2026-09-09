namespace MiniCode.Agent;

/// <summary>Where a single step of a plan has got to.</summary>
public enum TaskState
{
    /// <summary>Not started.</summary>
    Pending,

    /// <summary>Being worked on now. At most one step should be here.</summary>
    InProgress,

    /// <summary>Finished.</summary>
    Complete,

    /// <summary>Cannot proceed, and says why. The plan stops rather than guessing.</summary>
    Blocked,
}
