# Module 5b — Giving the Agent Read Access: Reading Files

## Project Overview

We give the agent the two tools it needs to explore a repository it has never seen: `list_files` and `read_file`. Both go through the sandbox from Module 4 and the interception seam from Module 5a, so this Module is mostly about a question the framework cannot answer for us — what should the model actually *see*?

## Prerequisites

**Starting point:** open Module05a-TheInterceptionSeam/.

Module 4's `IWorkspace`, and Module 5a's `IToolInvoker`, `InterceptedFunction` and `ToolCatalog`. The `workspace_root` tool from 5a stays exactly as it is.

Ships as part of **Phase 1 Capstone — Repository Explorer (v0.1)**.

## Setup

Nothing new. `Microsoft.Extensions.AI` is already in `MiniCode.Tools` from Module 5a, and `MiniCode.Workspace` still has no packages at all.

## Core Concepts

**A tool result is a prompt fragment, not a return value.** Whatever a tool returns is pasted into the conversation and becomes something the model reasons about. That reframes the design question: not "what is the correct data shape?" but "what text makes the model's next decision a good one?"

**Which is why these tools return strings.** A structured result object would only be serialised into text a moment later, and we would have chosen the shape without ever looking at what the model reads. Two projects, two jobs: `MiniCode.Workspace` decides what is *legal*, `MiniCode.Tools` decides what is *visible*.

**Line numbers earn their place.** `read_file` returns `    42  public void Save()` rather than bare source. It costs a few tokens per line and buys the ability for the model to say "line 42" and for a later Module to edit exactly there. This is the one formatting decision in the Module that pays off repeatedly.

**Everything must be capped, and the cap must be visible.** A repository has more files than fit in a context window, and one generated `.cs` file can be longer than the whole budget. So `list_files` stops at 200 entries and `read_file` at 400 lines — and when it truncates, it *says so* and tells the model how to ask for the next page. A silent truncation is worse than a refusal: the model concludes the file ends there and reasons confidently from half a file.

**Refusals cost us nothing extra, because Module 5a already solved it.** The service calls `ResolvePath`, which throws with the sandbox's reason. `DirectToolInvoker` catches it and returns `read_file failed: Path '../secrets' was refused: OutsideWorkspace.` — text the model can act on. We wrote no error-handling code in the tools at all; the seam's never-throw rule did the work.

**Tool descriptions are behaviour, not documentation.** `list_files` says *"Start here rather than guessing paths."* `read_file` says *"Prefer reading one file you have located over listing everything."* Those sentences are the only place the model is taught the search-before-read discipline, and Module 8 builds directly on the habit.

**Parameter descriptions matter as much as the tool's.** `AIFunctionFactory` reads `[Description]` on each parameter into the JSON schema. Without them the model guesses what `startLine` means; with them, paging through a long file is something it does unprompted.

## The Code

### `src/MiniCode.Workspace/IFileSystemService.cs`

```csharp
namespace MiniCode.Workspace;

/// <summary>
/// Reading the repository, inside the sandbox. Every path goes through
/// <see cref="IWorkspace"/> first, so there is no way to reach a file the
/// boundary would refuse.
/// </summary>
public interface IFileSystemService
{
    /// <summary>
    /// Lists files under a workspace-relative directory, as relative paths.
    /// Ignored directories are skipped rather than reported.
    /// </summary>
    IReadOnlyList<string> ListFiles(string? path, bool recursive = false);

    /// <summary>Finds lines matching a query, as <c>path:line: text</c>.</summary>
    IReadOnlyList<string> SearchFiles(string? query, string? path = null);

    /// <summary>Reads a text file, line-numbered so the model can cite locations.</summary>
    string ReadFile(string? path, int startLine = 1);
}
```

The two read methods this Module builds, from `src/MiniCode.Workspace/FileSystemService.cs`. `SearchFiles` is the Exercise; the rest of the file is the class declaration and the two caps:

```csharp
    public IReadOnlyList<string> ListFiles(string? path, bool recursive = false)
    {
        string directory = workspace.ResolvePath(string.IsNullOrWhiteSpace(path) ? "." : path);
        SearchOption depth = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        return [.. Directory.EnumerateFiles(directory, "*", depth)
            .Select(f => Path.GetRelativePath(workspace.Root, f).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(workspace.IsPathAllowed)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Take(MaxEntries)];
    }
    public string ReadFile(string? path, int startLine = 1)
    {
        string file = workspace.ResolvePath(path);
        string[] lines = File.ReadAllLines(file);
        int first = Math.Clamp(startLine, 1, Math.Max(lines.Length, 1));

        var text = new StringBuilder();
        foreach (int number in Enumerable.Range(first, Math.Min(MaxLines, lines.Length - first + 1)))
        {
            text.AppendLine($"{number,6}  {lines[number - 1]}");
        }

        int last = first + Math.Min(MaxLines, lines.Length - first + 1) - 1;
        if (last < lines.Length)
        {
            text.AppendLine($"... {lines.Length - last} more lines. Call again with startLine {last + 1}.");
        }

        return text.ToString();
    }
```

And their model-facing wrappers, from `src/MiniCode.Tools/FileTools.cs`:

```csharp
    public string ListFiles(
        [Description("Directory relative to the workspace root. Omit for the root itself.")] string? path = null,
        [Description("Include subdirectories.")] bool recursive = false)
    {
        IReadOnlyList<string> found = files.ListFiles(path, recursive);
        if (found.Count == 0)
        {
            return $"No files under '{path ?? "."}'.";
        }

        string listing = string.Join(Environment.NewLine, found);
        return found.Count < FileSystemService.MaxEntries
            ? listing
            : listing + Environment.NewLine
              + $"... {FileSystemService.MaxEntries} entries shown. List a subdirectory to narrow the results.";
    }
    public string ReadFile(
        [Description("File path relative to the workspace root.")] string path,
        [Description("First line to return. Use this to page through a long file.")] int startLine = 1)
        => files.ReadFile(path, startLine);
```

Each tool is registered through the same private `Intercept` from Module 5a. From `src/MiniCode.Tools/ToolCatalog.cs`:

```csharp
            Intercept(AIFunctionFactory.Create(
                tools.ListFiles,
                name: "list_files",
                description: "Lists files in the repository. Start here rather than guessing paths.")),
            Intercept(AIFunctionFactory.Create(
                tools.ReadFile,
                name: "read_file",
                description: "Reads a text file, line-numbered. Prefer reading one file you have "
                           + "located over listing everything.")),
```

## Walkthrough

1. **`ListFiles` treats an omitted path as the root**, so the model can call it with no arguments — which is what it does first, every time.
2. **`Where(workspace.IsPathAllowed)` filters the results**, so `bin`, `obj` and `.git` never appear. The sandbox is doing double duty: gatekeeper on the way in, filter on the way out.
3. **Relative paths, forward slashes.** The model sees `src/MiniCode.Cli/Program.cs` on every platform, and those strings are exactly what it can hand back to `read_file`.
4. **`ReadFile` clamps `startLine` instead of failing.** Ask for line 5,000 of a 40-line file and you get line 40, numbered — so the model can see from the number that the file is shorter than it assumed. A wrong-but-harmless argument should not cost a turn.
5. **Both truncation notices are instructions.** `... 812 more lines. Call again with startLine 401.` tells the model it has part of the file and exactly how to get the rest; the listing’s `... 200 entries shown. List a subdirectory to narrow the results.` does the same for a directory that is too large. Silence would leave the model believing it had seen everything.
6. **Neither tool contains a `try`.** `ResolvePath` throws, `DirectToolInvoker` catches, and the model reads a sentence naming the file and the reason. That is Module 5a's seam paying for itself immediately.
7. **`FileTools` takes `IFileSystemService`, not `IWorkspace`.** The tool layer cannot reach the filesystem directly even if someone tried.

## Exercise

**Implement `SearchFiles`** — the third tool the syllabus names, and the one that makes the search-before-read discipline possible.

Add `IReadOnlyList<string> SearchFiles(string? query, string? path = null)` to `IFileSystemService` and `FileSystemService`, then a `SearchFiles` method on `FileTools` and an entry in `ToolCatalog`.

Acceptance criteria:

- Returns matches as `path:line: text`, so one result tells the model where to read next.
- Searches only files the sandbox allows, and skips files it refuses without failing the whole call.
- Caps results the way the others do, and says so when it truncates.
- Case-insensitive plain-text matching is enough; a regex option is not required.
- Its description tells the model to search before reading — that sentence is the point of the tool.
- A query that matches nothing returns a sentence saying so, not an empty string. An empty result reads to the model as a broken tool.

The finished version is in **this Module’s folder** — `SearchFiles` on `IFileSystemService`, `FileSystemService` and `FileTools`, and a `search_files` entry in `ToolCatalog`. Write yours first, then compare. Module 8a builds directly on this tool, so it ships here rather than being left open.

## Expected Output

```text
> dotnet run --project src/MiniCode.Cli -- D:\Repos\CustomerPortal

Workspace: D:\Repos\CustomerPortal
MiniCode. Type 'exit' to quit.

> What kind of project is this, and where does it start?

It's a .NET solution. The entry point is src/CustomerPortal.Web/Program.cs,
which builds a WebApplication, registers services, and maps controllers. There
are three projects: CustomerPortal.Web, CustomerPortal.Core, and
CustomerPortal.Data.

> exit
```

To answer that, the model called `list_files` with no arguments, saw the three project directories, and read one file. Note what is *absent* from the listing it received: no `bin`, no `obj`, no `.git`. The sandbox filtered them before the model ever spent a token on them.
