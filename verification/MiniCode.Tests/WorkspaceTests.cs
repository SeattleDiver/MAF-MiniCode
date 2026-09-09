using Xunit;

using MiniCode.Workspace;

namespace MiniCode.Tests;

/// <summary>
/// Verifies the behaviour table published in Module 4's Expected Output.
/// This harness is ours; no test code appears in the course.
/// </summary>
public sealed class WorkspaceTests
{
    private static readonly string Root =
        OperatingSystem.IsWindows() ? @"D:\Repos\CustomerPortal" : "/repos/CustomerPortal";

    private static IWorkspace Sut() => new MiniCode.Workspace.Workspace(Root);

    [Theory]
    [InlineData("src/Program.cs")]
    [InlineData("./src/../src/Program.cs")]
    public void AllowsRelativePathsInsideTheRoot(string path) =>
        Assert.True(Sut().ValidatePath(path).IsAllowed);

    [Fact]
    public void AllowsTheRootItself() => Assert.True(Sut().ValidatePath(".").IsAllowed);

    [Theory]
    [InlineData("../../Windows/System32/drivers/etc/hosts")]
    [InlineData("src/../../../etc/passwd")]
    public void RefusesTraversalOutOfTheRoot(string path) =>
        Assert.Equal(PathRejectionReason.OutsideWorkspace, Sut().ValidatePath(path).Reason);

    [Fact]
    public void RefusesASiblingThatMerelySharesThePrefix()
    {
        // C:\repo-evil must not pass a startsWith test against C:\repo.
        IWorkspace sut = new MiniCode.Workspace.Workspace(Root);
        Assert.Equal(PathRejectionReason.OutsideWorkspace, sut.ValidatePath("../CustomerPortal-evil/secrets.txt").Reason);
    }

    [Theory]
    [InlineData("/Windows/System32")]
    [InlineData("C:notes.txt")]
    public void RefusesRootedInput(string path) =>
        Assert.Equal(PathRejectionReason.NotRelative, Sut().ValidatePath(path).Reason);

    [Fact]
    public void RefusesAnAbsolutePathEvenInsideTheWorkspace() =>
        Assert.Equal(PathRejectionReason.NotRelative,
            Sut().ValidatePath(Path.Combine(Root, "src", "Program.cs")).Reason);

    [Theory]
    [InlineData(".git/config")]
    [InlineData("src/obj/Debug/app.dll")]
    public void RefusesIgnoredDirectories(string path) =>
        Assert.Equal(PathRejectionReason.IgnoredDirectory, Sut().ValidatePath(path).Reason);

    [Fact]
    public void AllowsAFileWhoseNameMerelyStartsLikeAnIgnoredDirectory() =>
        Assert.True(Sut().ValidatePath("src/bind.cs").IsAllowed);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RefusesEmptyInput(string? path) =>
        Assert.Equal(PathRejectionReason.Empty, Sut().ValidatePath(path).Reason);

    [Fact]
    public void ResolvePathThrowsWhereValidateRefuses() =>
        Assert.Throws<InvalidOperationException>(() => Sut().ResolvePath("../escape.txt"));
}
