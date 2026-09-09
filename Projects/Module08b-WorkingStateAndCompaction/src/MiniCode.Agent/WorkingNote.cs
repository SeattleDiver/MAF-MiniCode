namespace MiniCode.Agent;

/// <summary>One fact worth carrying across a compaction boundary.</summary>
/// <param name="Kind">Which sort of fact it is.</param>
/// <param name="Text">The fact itself, already short enough to keep.</param>
public sealed record WorkingNote(WorkingNoteKind Kind, string Text);
