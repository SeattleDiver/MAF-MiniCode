# Module 10 — Safe File Editing

## Project Overview

Phase 3 begins, and MiniCode gets hands. It can now create a file and change one — and every design decision here is about making those two operations refuse rather than guess. An agent that edits confidently on stale information is worse than one that cannot edit at all.

## Prerequisites

**Starting point:** open `Phase2-Capstone-PlanningAgent/`.

Module 4's sandbox — every write goes through `ResolvePath` exactly as every read does — and Module 5b's `IFileSystemService`, which gains its write side here. Module 5a's interception seam matters too: it is what will let Module 15 put a human in front of these two tools without touching either of them.

First Module of Phase 3. Ships as part of **Phase 3 Capstone — Autonomous Fixer (v0.3)**.

## Setup

Nothing new to install. `System.Security.Cryptography` is in the shared framework, so `MiniCode.Workspace` still has zero package references.

## Core Concepts

**Prefer a targeted edit over rewriting a file.** Handing a model a whole file and taking back a whole file invites it to reformat, drop a using it did not understand, or silently lose the part it was not thinking about. Replacing one anchored fragment cannot do any of those.

**Deterministic means exactly once, or not at all.** `edit_file` finds the anchor text. Zero matches is a failure. *Two* matches is also a failure — because picking the first would be a guess, and a guess that edits source is the worst kind. The model is told to include more context and try again, which is a thing it is good at.

**Stale source is the failure that looks like success.** The model reads a file, thinks for four turns, then edits based on what it saw. Meanwhile the file changed — you edited it, a build regenerated it, another tool touched it. The edit still "works": the anchor is found, the write succeeds, and the result is wrong in a way nobody notices.

**So reads hand out a fingerprint and edits can demand it.** `read_file` now ends with twelve hex characters of a SHA-256 over the file's bytes. Pass it to `edit_file` and a file that has moved on is refused, with the new fingerprint in the message so the model knows to read again. Bytes, not a timestamp — timestamps have one-second granularity and change when nothing did.

**Creating a file and replacing one are different intentions.** `write_file` refuses an existing path unless `overwrite: true` is passed explicitly. The common case — write a new file — is safe by default, and destroying an existing one requires saying so.

**A refusal must be a sentence the model can act on.** *"That text appears more than once. Include enough context to make it unique."* is a usable instruction. `ArgumentException` is not. None of these methods contain a `try` — they throw, and Module 5a's `DirectToolInvoker` turns the throw into text, exactly as it has since the read tools.

**Showing the modification.** Both tools report what happened and hand back the new fingerprint: `Edited src/Customer.cs at line 7. Fingerprint 265f548e7c18.` The line number lets the model cite the change, and the fingerprint means the next edit can be guarded without reading the file again.

**What this Module deliberately does not do.** Nobody is asked for permission. MiniCode can now change your files because a language model decided to, and Module 15 is where a human finally gets a say. Encoding and line endings are the Exercise.

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

    /// <summary>Creates a file, or overwrites one deliberately. Returns lines written.</summary>
    int WriteFile(string? path, string? content, bool overwrite = false);

    /// <summary>
    /// Replaces one unique anchor. Returns the 1-based line the edit landed on.
    /// Refuses when the anchor is missing, appears twice, or the file has moved on.
    /// </summary>
    int EditFile(string? path, string? find, string? replace, string? expectedFingerprint = null);

    /// <summary>Twelve hex characters identifying the file’s current bytes.</summary>
    string Fingerprint(string? path);

    /// <summary>Reads a text file, line-numbered so the model can cite locations.</summary>
    string ReadFile(string? path, int startLine = 1);
}
```

Creating a file, from `src/MiniCode.Workspace/FileSystemService.cs`:

```csharp
    public int WriteFile(string? path, string? content, bool overwrite = false)
    {
        string file = workspace.ResolvePath(path);
        if (File.Exists(file) && !overwrite)
        {
            throw new InvalidOperationException(
                $"'{path}' already exists. Read it first, or pass overwrite: true to replace it.");
        }

        string text = content ?? string.Empty;
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, text);
        return text.Split('\n').Length;
    }
```

And changing one — the three refusals are most of the method:

```csharp
    public int EditFile(string? path, string? find, string? replace, string? expectedFingerprint = null)
    {
        string file = workspace.ResolvePath(path);

        if (expectedFingerprint is not null && Fingerprint(path) != expectedFingerprint)
        {
            throw new InvalidOperationException(
                $"'{path}' has changed since you read it (now {Fingerprint(path)}). Read it again.");
        }

        string anchor = find ?? string.Empty;
        string text = File.ReadAllText(file);
        int first = text.IndexOf(anchor, StringComparison.Ordinal);

        if (anchor.Length == 0 || first < 0)
        {
            throw new InvalidOperationException($"That text does not appear in '{path}'.");
        }

        if (text.IndexOf(anchor, first + 1, StringComparison.Ordinal) >= 0)
        {
            throw new InvalidOperationException(
                $"That text appears more than once in '{path}'. Include enough context to make it unique.");
        }

        File.WriteAllText(file, string.Concat(text[..first], replace, text[(first + anchor.Length)..]));
        return text[..first].Count(c => c == '\n') + 1;
    }
```

The fingerprint itself is one line, and `ReadFile` now appends it:

```csharp
    public string Fingerprint(string? path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(workspace.ResolvePath(path))))[..12].ToLowerInvariant();
```

`FileTools` wraps both, reporting the line and the new fingerprint, and `ToolCatalog` registers `write_file` and `edit_file` through the same private `Intercept` as every other tool.

## Walkthrough

1. **`ResolvePath` is the first line of both methods.** Writing outside the workspace is not a special case that needed new code — it is the same sandbox, refusing the same way it has since Module 4.
2. **The fingerprint check runs before the file is read**, so a stale edit costs nothing beyond hashing.
3. **`expectedFingerprint` is optional and defaults to null.** An edit without one still works — this is a guard the model opts into, and `read_file` tells it to. Making it mandatory would break the case where the model writes a file and immediately edits it.
4. **Twelve hex characters, not sixty-four.** The model copies this string; every character costs context. Twelve is ample for detecting change, and this is a staleness check rather than a security boundary.
5. **The uniqueness check uses `IndexOf` from `first + 1`** rather than counting every occurrence. It only matters whether there is a second one.
6. **The replacement is `string.Concat` on three slices.** No regex, no line splitting — so whatever line endings surrounded the anchor are still there afterwards, untouched, because they were never parsed.
7. **`Directory.CreateDirectory` before writing** means `write_file` can create `src/Models/Customer.cs` when `Models` does not exist yet, which is what creating a file usually means in practice.
8. **Module 8b's working state now knows the difference.** A `write_file` or `edit_file` call records `src/Customer.cs (modified)` rather than the plain path a read produces, and the modified note supersedes the read note for the same file. Without that, a compaction leaves the agent knowing it *looked at* a file it had actually changed — which is how an agent redoes work it already did.

## Exercise

**One:** preserve encoding and line endings. `WriteFile` uses `File.WriteAllText`, which writes UTF-8 without a BOM and whatever line endings were in the string. Overwrite a CRLF file with content the model composed using `\n` and you get a diff touching every line — a catastrophe that looks like a formatting change. Detect the existing file's encoding (including whether it has a BOM) and its dominant line ending before writing, and re-apply both. Acceptance criteria: overwriting a CRLF file with LF content leaves the file CRLF; a UTF-8-with-BOM file still has its BOM afterwards; a new file gets UTF-8 without a BOM and the platform's line ending. This belongs in `src/MiniCode.Workspace/FileSystemService.cs`.

**Two:** show the modification as a diff. Both tools currently report a line number. Return a few lines of before-and-after context instead — the changed region with two lines either side, `-` and `+` prefixed — so the operator can see on screen what changed without opening the file. Write it over the BCL; do not add a diff package. Acceptance criteria: an edit inside a large file shows only the changed region; an edit at line 1 does not fail trying to show context above it; the output is short enough to be worth putting in the model's context.

Neither has a reference implementation in a later Module folder.

## Expected Output

All of the following is real output from this Module's code, against a temporary repository containing one `Customer.cs`.

A read now ends with a fingerprint:

```text
     1  namespace Shop;
     2  
     3  public sealed class Customer
     4  {
     5      public int Id { get; set; }
     6  
     7      public string Name { get; set; } = string.Empty;
     8  }
Fingerprint f260c10b23b8 - pass this as expectedFingerprint when editing.
```

The lab's edit — *add an EmailAddress property to Customer* — anchored on the `Name` property:

```text
Edited src/Customer.cs at line 7. Fingerprint 265f548e7c18.
```

Then the three refusals, each one a sentence the model can act on:

```text
edit_file failed: 'src/Customer.cs' has changed since you read it (now 265f548e7c18). Read it again.
edit_file failed: That text appears more than once in 'src/Customer.cs'. Include enough context to make it unique.
write_file failed: 'src/Customer.cs' already exists. Read it first, or pass overwrite: true to replace it.
```

The first is the important one. That fingerprint was valid a moment earlier; the edit that used it changed the file, so the *next* edit carrying the same fingerprint is refused. Creating a new file is unremarkable, which is the point:

```text
Wrote src/Order.cs (4 lines). Fingerprint e4d10c4aa332.
```

And the file on disk afterwards:

```text
namespace Shop;

public sealed class Customer
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string EmailAddress { get; set; } = string.Empty;
}
```

One property added, blank lines and indentation intact, nothing else in the file touched. That is what a targeted edit buys you over handing the model the whole file and hoping.
