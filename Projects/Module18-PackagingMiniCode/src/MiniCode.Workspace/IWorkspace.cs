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
