using Microsoft.Extensions.AI;
using MiniCode.Tools;
using MiniCode.Workspace;
using Xunit;

namespace MiniCode.Tests;

/// <summary>Verifies the read tools and the interception seam. Ours, not the course's.</summary>
public sealed class ReadAccessTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("minicode").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private (IWorkspace Ws, IFileSystemService Files) Build()
    {
        Directory.CreateDirectory(Path.Combine(_root, "src"));
        Directory.CreateDirectory(Path.Combine(_root, "obj"));
        File.WriteAllLines(Path.Combine(_root, "src", "Program.cs"), ["one", "two", "three"]);
        File.WriteAllText(Path.Combine(_root, "obj", "generated.cs"), "noise");
        IWorkspace ws = new MiniCode.Workspace.Workspace(_root);
        return (ws, new FileSystemService(ws));
    }

    [Fact]
    public void ListFilesSkipsIgnoredDirectories()
    {
        var (_, files) = Build();
        IReadOnlyList<string> found = files.ListFiles(null, recursive: true);
        Assert.Contains("src/Program.cs", found);
        Assert.DoesNotContain(found, f => f.Contains("obj/"));
    }

    [Fact]
    public void ReadFileNumbersLines()
    {
        var (_, files) = Build();
        Assert.Contains("     2  two", files.ReadFile("src/Program.cs"));
    }

    [Fact]
    public void ReadFileClampsAnOutOfRangeStartLine()
    {
        var (_, files) = Build();
        // Clamped to the last line, and the line number shows the file is shorter.
        Assert.Contains("     3  three", files.ReadFile("src/Program.cs", startLine: 9999));
    }

    [Fact]
    public void ReadingOutsideTheWorkspaceThrowsWithAReason()
    {
        var (_, files) = Build();
        InvalidOperationException ex =
            Assert.Throws<InvalidOperationException>(() => files.ReadFile("../escape.txt"));
        Assert.Contains("OutsideWorkspace", ex.Message);
    }

    [Fact]
    public async Task TheInvokerTurnsAThrowIntoTextTheModelCanRead()
    {
        var (_, files) = Build();
        var tools = new FileTools(files);
        AIFunction fn = AIFunctionFactory.Create(tools.ReadFile, name: "read_file", description: "x");

        object? result = await new DirectToolInvoker()
            .InvokeAsync(new ToolInvocation(fn, new AIFunctionArguments { ["path"] = "../escape.txt" }),
                         TestContext.Current.CancellationToken);

        Assert.Contains("read_file failed:", Assert.IsType<string>(result));
        Assert.Contains("OutsideWorkspace", (string)result!);
    }

    [Fact]
    public async Task AnInvokerCanShortCircuitAndTheToolNeverRuns()
    {
        var (_, files) = Build();
        var tools = new FileTools(files);
        AIFunction inner = AIFunctionFactory.Create(tools.ReadFile, name: "read_file", description: "x");
        var wrapper = new InterceptedFunction(inner, new RefusingInvoker());

        object? result = await wrapper.InvokeAsync(
            new AIFunctionArguments { ["path"] = "src/Program.cs" },
            TestContext.Current.CancellationToken);

        Assert.Equal("refused", result);
    }

    [Fact]
    public void EveryCatalogToolIsWrapped()
    {
        var (ws, files) = Build();
        IReadOnlyList<AITool> tools = new ToolCatalog(ws, files, new RepositoryInspector(ws), new DirectToolInvoker()).GetTools();
        Assert.Equal(5, tools.Count);
        Assert.All(tools, t => Assert.IsType<InterceptedFunction>(t));
    }

    private sealed class RefusingInvoker : IToolInvoker
    {
        public ValueTask<object?> InvokeAsync(ToolInvocation invocation, CancellationToken ct) =>
            ValueTask.FromResult<object?>("refused");
    }

    [Fact]
    public void ListingSaysSoWhenItHitsTheCap()
    {
        Build();
        for (int i = 0; i < FileSystemService.MaxEntries + 5; i++)
        {
            File.WriteAllText(Path.Combine(_root, "src", $"F{i}.cs"), "x");
        }

        IWorkspace ws = new MiniCode.Workspace.Workspace(_root);
        string listing = new FileTools(new FileSystemService(ws)).ListFiles("src");
        Assert.Contains("entries shown", listing);
    }
}
