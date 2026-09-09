using Microsoft.Extensions.AI;
using MiniCode.Infrastructure;
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
    IShellExecutor shell,
    IToolInvoker invoker)
{
    /// <summary>The tools to hand to the agent.</summary>
    public IReadOnlyList<AITool> GetTools()
    {
        var fileTools = new FileTools(files);
        var repositoryTools = new RepositoryTools(inspector, files);
        var shellTools = new ShellTools(shell, workspace);

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
                fileTools.WriteFile,
                name: "write_file",
                description: "Creates a file. Refuses to overwrite an existing one unless you "
                           + "deliberately ask, so it cannot clobber work by accident.")),
            Intercept(AIFunctionFactory.Create(
                fileTools.EditFile,
                name: "edit_file",
                description: "Replaces one unique piece of text in a file. Read the file first "
                           + "and pass the fingerprint it gave you, so a stale edit is refused.")),
            Intercept(AIFunctionFactory.Create(
                fileTools.ReadFile,
                name: "read_file",
                description: "Reads a text file, line-numbered. Prefer reading one file you have "
                           + "located over listing everything.")),
            Intercept(AIFunctionFactory.Create(
                shellTools.RunCommand,
                name: "run_command",
                description: "Runs a program with arguments in the workspace root, e.g. command: "
                           + "\"dotnet\", arguments: [\"build\"]. Not passed through a shell. Killed "
                           + "and reported if it runs past the timeout. Restricted to a fixed allow "
                           + "list; a refusal names what is permitted. For dotnet test, the summary "
                           + "and failing tests are reported; passing tests are not listed by name.")),
        ];
    }

    private AITool Intercept(AIFunction function) => new InterceptedFunction(function, invoker);
}
