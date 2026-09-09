using Microsoft.Extensions.AI;
using MiniCode.Workspace;

namespace MiniCode.Tools;

/// <summary>
/// Builds the tools the model may call. Every tool leaves here already wrapped
/// in an <see cref="InterceptedFunction"/>, so there is no code path that
/// produces an unwrapped one.
/// </summary>
public sealed class ToolCatalog(
    IWorkspace workspace,
    IFileSystemService files,
    IRepositoryInspector inspector,
    IToolInvoker invoker)
{
    /// <summary>The tools to hand to the agent.</summary>
    public IReadOnlyList<AITool> GetTools()
    {
        var fileTools = new FileTools(files);
        var repositoryTools = new RepositoryTools(inspector, files);

        return
        [
            Intercept(AIFunctionFactory.Create(
                () => workspace.Root,
                name: "workspace_root",
                description: "Returns the absolute path of the repository MiniCode is working in. "
                           + "Every other tool takes paths relative to this root.")),
            Intercept(AIFunctionFactory.Create(
                repositoryTools.DescribeRepository,
                name: "describe_repository",
                description: "Summarises the solution: its projects, target frameworks, project "
                           + "references and NuGet packages. Call this first to learn the shape "
                           + "of an unfamiliar repository.")),
            Intercept(AIFunctionFactory.Create(
                fileTools.ListFiles,
                name: "list_files",
                description: "Lists files in the repository. Start here rather than guessing paths.")),
            Intercept(AIFunctionFactory.Create(
                fileTools.SearchFiles,
                name: "search_files",
                description: "Searches file contents and returns path:line: text. Search before "
                           + "reading: one match tells you which file to open.")),
            Intercept(AIFunctionFactory.Create(
                fileTools.ReadFile,
                name: "read_file",
                description: "Reads a text file, line-numbered. Prefer reading one file you have "
                           + "located over listing everything.")),
        ];
    }

    private AITool Intercept(AIFunction function) => new InterceptedFunction(function, invoker);
}
