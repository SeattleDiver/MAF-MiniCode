using Microsoft.Extensions.AI;

namespace MiniCode.Agent;

/// <summary>
/// What MiniCode knows, distilled from a stretch of conversation that is about
/// to be thrown away. Deriving this is deterministic — no model call, so a
/// compaction cannot fail or cost a request.
/// </summary>
public sealed class WorkingState
{
    private const int MaxNoteLength = 160;

    private WorkingState(IReadOnlyList<WorkingNote> notes) => Notes = notes;

    /// <summary>The notes, in the order they were observed.</summary>
    public IReadOnlyList<WorkingNote> Notes { get; }

    /// <summary>True when there was nothing worth keeping.</summary>
    public bool IsEmpty => Notes.Count == 0;

    /// <summary>Reads a run of messages and keeps only what must survive.</summary>
    public static WorkingState From(IEnumerable<ChatMessage> messages)
    {
        var notes = new List<WorkingNote>();

        foreach (ChatMessage message in messages)
        {
            foreach (FunctionCallContent call in message.Contents.OfType<FunctionCallContent>())
            {
                Add(notes, WorkingNoteKind.FileState, FileNote(call));
            }

            if (message.Role == ChatRole.Tool)
            {
                // Tool output is the bulkiest thing in the window and the most
                // disposable: the file it came from is already noted above.
                continue;
            }

            Add(notes,
                message.Role == ChatRole.User ? WorkingNoteKind.OpenTask : WorkingNoteKind.CompletedWork,
                message.Text);
        }

        return new WorkingState(notes);
    }

    /// <summary>Renders the state as the one message that replaces the run.</summary>
    public string Render()
    {
        var sections = new List<string>();

        foreach (WorkingNoteKind kind in Enum.GetValues<WorkingNoteKind>())
        {
            List<WorkingNote> section = [.. Notes.Where(n => n.Kind == kind)];
            if (section.Count > 0)
            {
                sections.Add(Label(kind) + Environment.NewLine
                    + string.Join(Environment.NewLine, section.Select(n => "- " + n.Text)));
            }
        }

        string blank = Environment.NewLine + Environment.NewLine;
        return "SESSION STATE (earlier turns were compacted away)" + blank + string.Join(blank, sections);
    }

    private static void Add(List<WorkingNote> notes, WorkingNoteKind kind, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string note = text.Trim().ReplaceLineEndings(" ");
        note = note.Length > MaxNoteLength ? note[..MaxNoteLength] + "..." : note;

        if (kind == WorkingNoteKind.FileState && note.EndsWith(" (modified)", StringComparison.Ordinal))
        {
            notes.RemoveAll(n => n.Kind == kind && n.Text == note[..^11]);
        }

        if (!notes.Any(n => n.Kind == kind && n.Text == note))
        {
            notes.Add(new WorkingNote(kind, note));
        }
    }

    // A file the agent changed is not the same fact as one it merely read, and
    // losing that distinction across a compaction is how an agent redoes work.
    private static string? FileNote(FunctionCallContent call) =>
        PathArgument(call) is not { } path ? null
            : call.Name is "write_file" or "edit_file" ? $"{path} (modified)" : path;

    private static string? PathArgument(FunctionCallContent call) =>
        call.Arguments?.TryGetValue("path", out object? value) == true ? value?.ToString() : null;

    private static string Label(WorkingNoteKind kind) => kind switch
    {
        WorkingNoteKind.OpenTask => "What was asked:",
        WorkingNoteKind.CompletedWork => "What was established:",
        _ => "Files already examined:",
    };
}
