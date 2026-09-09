using Microsoft.Extensions.AI;
using MiniCode.Agent;
using Xunit;

namespace MiniCode.Tests;

/// <summary>Verifies what survives a compaction. Ours, not the course's.</summary>
public sealed class CompactionTests
{
    private static ChatMessage Read(string path) =>
        new(ChatRole.Assistant, [new FunctionCallContent("c", "read_file",
            new Dictionary<string, object?> { ["path"] = path })]);

    [Fact]
    public void AModifiedFileIsNotTheSameFactAsAReadFile()
    {
        WorkingState state = WorkingState.From(
        [
            new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("c1", "read_file",
                new Dictionary<string, object?> { ["path"] = "src/A.cs" })]),
            new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("c2", "edit_file",
                new Dictionary<string, object?> { ["path"] = "src/A.cs" })]),
        ]);

        WorkingNote note = Assert.Single(state.Notes, n => n.Kind == WorkingNoteKind.FileState);
        Assert.Equal("src/A.cs (modified)", note.Text);
    }

    [Fact]
    public void ToolOutputIsDiscardedRatherThanSummarised()
    {
        WorkingState state = WorkingState.From(
        [
            new ChatMessage(ChatRole.User, "where is X?"),
            Read("src/A.cs"),
            new ChatMessage(ChatRole.Tool, new string('x', 5000)),
        ]);

        Assert.DoesNotContain(state.Notes, n => n.Text.StartsWith("xxx", StringComparison.Ordinal));
        Assert.Contains(state.Notes, n => n.Kind == WorkingNoteKind.FileState && n.Text == "src/A.cs");
    }

    [Fact]
    public void KeepsTheRequestTheAnswerAndTheFile()
    {
        WorkingState state = WorkingState.From(
        [
            new ChatMessage(ChatRole.User, "where is X?"),
            Read("src/A.cs"),
            new ChatMessage(ChatRole.Assistant, "X is in A."),
        ]);

        Assert.Contains(state.Notes, n => n.Kind == WorkingNoteKind.OpenTask && n.Text == "where is X?");
        Assert.Contains(state.Notes, n => n.Kind == WorkingNoteKind.CompletedWork && n.Text == "X is in A.");
        Assert.Contains(state.Notes, n => n.Kind == WorkingNoteKind.FileState && n.Text == "src/A.cs");
    }

    [Fact]
    public void DeduplicatesRepeatedFacts()
    {
        WorkingState state = WorkingState.From(
            [new ChatMessage(ChatRole.User, "same"), new ChatMessage(ChatRole.User, "same")]);

        Assert.Single(state.Notes);
    }

    [Fact]
    public void TruncatesALongNote()
    {
        WorkingState state = WorkingState.From([new ChatMessage(ChatRole.Assistant, new string('y', 500))]);
        Assert.EndsWith("...", Assert.Single(state.Notes).Text);
        Assert.True(Assert.Single(state.Notes).Text.Length < 200);
    }

    [Fact]
    public void RenderOrdersSectionsAskedEstablishedExamined()
    {
        string rendered = WorkingState.From(
        [
            new ChatMessage(ChatRole.User, "q"),
            Read("src/A.cs"),
            new ChatMessage(ChatRole.Assistant, "a"),
        ]).Render();

        int asked = rendered.IndexOf("What was asked", StringComparison.Ordinal);
        int established = rendered.IndexOf("What was established", StringComparison.Ordinal);
        int examined = rendered.IndexOf("Files already examined", StringComparison.Ordinal);
        Assert.True(asked < established && established < examined);
    }

    [Fact]
    public void AnEmptyRunIsEmpty() => Assert.True(WorkingState.From([]).IsEmpty);
}
