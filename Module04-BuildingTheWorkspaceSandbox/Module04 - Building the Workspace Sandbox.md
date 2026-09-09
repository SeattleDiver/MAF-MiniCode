# Module 4 — Building the Workspace Sandbox

## Project Overview

We give MiniCode a boundary before we give it hands. `MiniCode.Workspace` decides whether a path the model supplied is inside the repository — and refuses everything else. This is the class every file operation in the rest of the course goes through, and it is deliberately the smallest, dullest, most testable thing in the solution.

## Prerequisites

**Starting point:** open Module03-DesigningTheMiniCodeSolution/.

Module 3's five projects and their dependency direction. `MiniCode.Workspace` is one of the two leaves — no package references, no project references — and this Module is what fills it.

Ships as part of **Phase 1 Capstone — Repository Explorer (v0.1)**.

## Setup

Nothing new. `MiniCode.Workspace` needs no packages; everything here comes from `System.IO` in the base class library. That is not an accident — a dependency you have not read is not a boundary, it is a hope.

## Core Concepts

**Why the boundary comes before the capability.** In two Modules the agent gets tools that read files, and in Module 10 tools that write them. The path those tools receive is a string a language model produced. It may be a mistake, or it may be text a repository's own README told the model to use. Either way, one class has to be the thing that says no — and it has to be small enough that you can read it and believe it.

**The workspace root is the only absolute path in the system.** Everything the agent says is relative to it. That single convention is what makes the rest checkable: if the agent can only express relative paths, escaping means producing a relative path that resolves outside the root, and there are only a handful of ways to try that.

**Normalize, then compare. Never compare raw strings.** `../../etc/passwd` and `src/../../../etc/passwd` are the same place. `Path.GetFullPath` is what collapses `..` and settles separators, and comparison only means anything afterwards.

**The trailing separator is the subtle one.** If the root is `C:\repo` and you check whether a resolved path starts with `C:\repo`, then `C:\repo-evil\secrets.txt` passes — it really does start with those characters. Comparing against `C:\repo\` closes it. This is a one-character bug that hands over the whole filesystem.

**Rooted input must be refused before it is combined, not after.** `Path.Combine("C:\repo", "\Windows\System32")` returns `\Windows\System32` — it *discards* the root when the second argument is rooted. So a path like `\Windows` never looks like traversal at all; there is no `..` to catch. It has to be rejected on the way in.

**Refusing is not an error.** `ValidatePath` returns a result and never throws, because from Module 5b its caller is a tool that must hand the model a sentence it can act on. A thrown exception in that position costs the agent its turn and tells it nothing. `ResolvePath` throws, for the internal callers who treat a refusal as a bug.

**Some directories are inside the root and still off limits.** `.git`, `bin`, `obj`, `node_modules`. `.git` is the dangerous one: an agent writing there can rewrite history rather than code.

**Case sensitivity is the platform's business, not ours.** `Program.cs` and `program.cs` are one file on Windows and two on Linux. Choose the comparison from `OperatingSystem.IsWindows()` rather than picking one and being wrong half the time.

## The Code

### `src/MiniCode.Workspace/PathRejectionReason.cs`

```csharp
namespace MiniCode.Workspace;

/// <summary>Why a path was refused. Carried on <see cref="PathValidationResult"/>.</summary>
public enum PathRejectionReason
{
    None = 0,
    Empty,
    /// <summary>Rooted where a workspace-relative path was expected.</summary>
    NotRelative,
    OutsideWorkspace,
    /// <summary>Inside a directory MiniCode never touches, such as <c>.git</c>.</summary>
    IgnoredDirectory,
    /// <summary>Illegal characters, or too long for the platform.</summary>
    Malformed,
}
```

### `src/MiniCode.Workspace/PathValidationResult.cs`

```csharp
namespace MiniCode.Workspace;

/// <summary>
/// The outcome of checking a path. Never throws, so a tool can turn a refusal
/// into a sentence the model can act on. <c>FullPath</c> is empty when refused.
/// </summary>
public sealed record PathValidationResult(bool IsAllowed, string FullPath, PathRejectionReason Reason)
{
    internal static PathValidationResult Allow(string fullPath) => new(true, fullPath, PathRejectionReason.None);

    internal static PathValidationResult Refuse(PathRejectionReason reason) => new(false, string.Empty, reason);
}
```

### `src/MiniCode.Workspace/IWorkspace.cs`

```csharp
namespace MiniCode.Workspace;

/// <summary>
/// The repository boundary. Every path the agent supplies passes through here
/// before anything opens a file. Paths are nullable on purpose: from Module 5b
/// these values arrive from a language model and may be anything at all.
/// </summary>
public interface IWorkspace
{
    /// <summary>The workspace root, fully resolved.</summary>
    string Root { get; }

    /// <summary>Checks a workspace-relative path. Never throws.</summary>
    PathValidationResult ValidatePath(string? path);

    /// <summary>Resolves to an absolute path, or throws if refused.</summary>
    string ResolvePath(string? path);

    bool IsPathAllowed(string? path);
}
```

### `src/MiniCode.Workspace/Workspace.cs`

```csharp
namespace MiniCode.Workspace;

/// <summary>
/// The sandbox. It is pure path arithmetic — nothing here touches the disk — so
/// the question "can the agent reach this file?" is answered by one small class.
/// </summary>
public sealed class Workspace : IWorkspace
{
    private static readonly string[] IgnoredDirectories = [".git", "bin", "obj", "node_modules"];

    private static readonly StringComparison Comparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly string _rootWithSeparator;

    public Workspace(string root)
    {
        Root = Path.GetFullPath(root);
        _rootWithSeparator = Root.EndsWith(Path.DirectorySeparatorChar)
            ? Root : Root + Path.DirectorySeparatorChar;
    }

    /// <inheritdoc />
    public string Root { get; }

    /// <inheritdoc />
    public PathValidationResult ValidatePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return PathValidationResult.Refuse(PathRejectionReason.Empty);
        }

        // Refuse rooted input before combining. Path.Combine silently discards
        // the root when its second argument is rooted, so "\Windows\System32"
        // would otherwise escape without ever looking like traversal.
        if (Path.IsPathRooted(path) || path.Contains(':'))
        {
            return PathValidationResult.Refuse(PathRejectionReason.NotRelative);
        }

        string fullPath;
        try
        {
            // GetFullPath is what collapses ".." and normalizes separators.
            fullPath = Path.GetFullPath(Path.Combine(Root, path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return PathValidationResult.Refuse(PathRejectionReason.Malformed);
        }

        // Compare against the root *with* its trailing separator, so a sibling
        // that merely shares the prefix — C:\repo-evil next to C:\repo — fails.
        if (!fullPath.StartsWith(_rootWithSeparator, Comparison) && !fullPath.Equals(Root, Comparison))
        {
            return PathValidationResult.Refuse(PathRejectionReason.OutsideWorkspace);
        }

        string relative = Path.GetRelativePath(Root, fullPath);
        string[] segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(s => IgnoredDirectories.Contains(s, StringComparer.FromComparison(Comparison)))
            ? PathValidationResult.Refuse(PathRejectionReason.IgnoredDirectory)
            : PathValidationResult.Allow(fullPath);
    }

    /// <inheritdoc />
    public string ResolvePath(string? path)
    {
        PathValidationResult result = ValidatePath(path);
        return result.IsAllowed
            ? result.FullPath
            : throw new InvalidOperationException($"Path '{path}' was refused: {result.Reason}.");
    }

    /// <inheritdoc />
    public bool IsPathAllowed(string? path) => ValidatePath(path).IsAllowed;
}
```

## Walkthrough

1. **The constructor resolves the root once** and keeps a second copy with a trailing separator. Two fields, and the second one is the whole prefix defence.
2. **`ValidatePath` refuses in the order the attacks arrive.** Empty first, then rooted input, then normalization, then the prefix comparison, then the ignore list. Each check is cheap and each one is a different mistake.
3. **Rooted input is caught before `Path.Combine` ever sees it.** `Path.IsPathRooted` catches `\Windows` and `/etc`; the `':'` check catches Windows drive-relative forms like `C:notes.txt`.
4. **`GetFullPath` does the normalization**, and it is wrapped in a `try` because a path with illegal characters throws rather than returning something useless. That becomes `Malformed`, not a crash.
5. **The prefix check tests the separator-terminated root**, plus an equality case so the root itself is allowed.
6. **The ignore list runs over path segments, not the whole string** — so a file legitimately called `bind.cs` is fine, while `src/bin/tool.dll` is not.
7. **`ResolvePath` and `IsPathAllowed` are one-liners over `ValidatePath`.** Three public members, one implementation of the decision — which is what makes this class believable.

## Exercise

**One:** wire the root into startup. `Program.cs` currently constructs the agent and nothing else. Take an optional first command-line argument as the workspace root, default to the current directory, construct a `Workspace` from it, and print `Workspace: <root>` under the banner. Note the type is `MiniCode.Workspace.Workspace` — the class and its namespace share a name, so consumers qualify it. Module 5a is where the workspace is handed to the first tool — compare your version against `src/MiniCode.Cli/Program.cs` in `Module05a-TheInterceptionSeam/` when you get there.

**Two:** add binary-file detection. Add `bool IsBinaryFile(string? path)` to `IWorkspace` and `Workspace`. Read the first 8 KB and treat the file as binary if it contains a NUL byte. Acceptance criteria: a `.cs` file is not binary; a `.dll` is; a file outside the workspace is refused rather than answered; a file that does not exist is not an exception.

**Three (stretch):** support `.gitignore`. Read the file at the workspace root and refuse paths it matches, with a new `PathRejectionReason`. Handle only leading-slash anchoring, trailing-slash directory patterns, and `*` within a segment — and say in your own comments which real gitignore features you are not implementing. Acceptance criteria: a pattern `build/` refuses `build/out.txt` but not `src/build.cs`; a pattern `*.tmp` refuses `notes.tmp` at any depth; an empty or missing `.gitignore` refuses nothing.

## Expected Output

The chat loop is untouched, so MiniCode still behaves exactly as it did in Module 3 — the sandbox is not wired to anything until Module 5a. What the class does is best seen by checking a handful of paths against a root of `D:\Repos\CustomerPortal`:

| `path` | `IsAllowed` | `Reason` |
|---|---|---|
| `src/Program.cs` | true | `None` |
| `./src/../src/Program.cs` | true | `None` |
| `..\..\Windows\System32\drivers\etc\hosts` | false | `OutsideWorkspace` |
| `\Windows\System32` | false | `NotRelative` |
| `D:\Repos\CustomerPortal\src\Program.cs` | false | `NotRelative` |
| `C:notes.txt` | false | `NotRelative` |
| `.git/config` | false | `IgnoredDirectory` |
| `src/obj/Debug/app.dll` | false | `IgnoredDirectory` |
| *(empty string)* | false | `Empty` |

The row worth pausing on is the third from bottom. An absolute path *inside* the workspace is still refused, because the agent's contract is that it speaks in relative paths. Accepting absolute paths "when they happen to be inside" is how a sandbox acquires a second code path — and a second code path is how it eventually acquires a hole.
