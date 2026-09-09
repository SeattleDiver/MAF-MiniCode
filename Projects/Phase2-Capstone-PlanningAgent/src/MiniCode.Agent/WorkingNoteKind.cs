namespace MiniCode.Agent;

/// <summary>What a working note is about. These are the things that must survive
/// compaction — everything else in the conversation is expendable.</summary>
public enum WorkingNoteKind
{
    /// <summary>Something the operator asked for. Kept until it is clearly done.</summary>
    OpenTask,

    /// <summary>A conclusion MiniCode reached, including any decision it explained.</summary>
    CompletedWork,

    /// <summary>A file MiniCode has already looked at.</summary>
    FileState,
}
