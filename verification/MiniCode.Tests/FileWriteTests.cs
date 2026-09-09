using MiniCode.Tools;
using MiniCode.Workspace;
using Xunit;

namespace MiniCode.Tests;

/// <summary>Verifies the write side refuses rather than guesses. Ours, not the course's.</summary>
public sealed class FileWriteTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("minicode").FullName;
    private readonly IFileSystemService _files;

    public FileWriteTests() => _files = new FileSystemService(new MiniCode.Workspace.Workspace(_root));

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Seed(string content) => File.WriteAllText(Path.Combine(_root, "a.cs"), content);

    [Fact]
    public void CreatesANewFileAndAnyMissingDirectories()
    {
        Assert.Equal(1, _files.WriteFile("src/deep/New.cs", "x"));
        Assert.True(File.Exists(Path.Combine(_root, "src", "deep", "New.cs")));
    }

    [Fact]
    public void RefusesToOverwriteUnlessAsked()
    {
        Seed("original");
        Assert.Throws<InvalidOperationException>(() => _files.WriteFile("a.cs", "replacement"));
        Assert.Equal("original", File.ReadAllText(Path.Combine(_root, "a.cs")));

        _files.WriteFile("a.cs", "replacement", overwrite: true);
        Assert.Equal("replacement", File.ReadAllText(Path.Combine(_root, "a.cs")));
    }

    [Fact]
    public void EditsAUniqueAnchorAndReportsItsLine()
    {
        Seed("one\ntwo\nthree\n");
        Assert.Equal(2, _files.EditFile("a.cs", "two", "TWO"));
        Assert.Equal("one\nTWO\nthree\n", File.ReadAllText(Path.Combine(_root, "a.cs")));
    }

    [Fact]
    public void RefusesAnAmbiguousAnchorAndChangesNothing()
    {
        Seed("x\nx\n");
        Assert.Throws<InvalidOperationException>(() => _files.EditFile("a.cs", "x", "y"));
        Assert.Equal("x\nx\n", File.ReadAllText(Path.Combine(_root, "a.cs")));
    }

    [Fact]
    public void RefusesAnAnchorThatIsNotThere()
    {
        Seed("one\n");
        Assert.Throws<InvalidOperationException>(() => _files.EditFile("a.cs", "missing", "y"));
    }

    [Fact]
    public void RefusesAStaleEdit()
    {
        Seed("one\ntwo\n");
        string stale = _files.Fingerprint("a.cs");
        _files.EditFile("a.cs", "one", "ONE");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => _files.EditFile("a.cs", "two", "TWO", stale));
        Assert.Contains("has changed since you read it", ex.Message);
        Assert.Contains("two", File.ReadAllText(Path.Combine(_root, "a.cs")));
    }

    [Fact]
    public void AFingerprintTracksTheBytes()
    {
        Seed("one");
        string before = _files.Fingerprint("a.cs");
        Assert.Equal(before, _files.Fingerprint("a.cs"));

        _files.WriteFile("a.cs", "two", overwrite: true);
        Assert.NotEqual(before, _files.Fingerprint("a.cs"));
    }

    [Fact]
    public void WritingOutsideTheWorkspaceIsRefused() =>
        Assert.Throws<InvalidOperationException>(() => _files.WriteFile("../escape.cs", "x"));

    [Fact]
    public void ReadingHandsBackAFingerprintToEditWith()
    {
        Seed("one\n");
        Assert.Contains($"Fingerprint {_files.Fingerprint("a.cs")}", _files.ReadFile("a.cs"));
    }
}
