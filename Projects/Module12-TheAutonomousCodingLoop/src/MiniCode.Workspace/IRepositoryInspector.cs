namespace MiniCode.Workspace;

/// <summary>
/// Reads the repository's shape from its project files. Deterministic — it
/// parses XML rather than asking a model what it thinks is there.
/// </summary>
public interface IRepositoryInspector
{
    /// <summary>Every project the sandbox allows, in path order.</summary>
    IReadOnlyList<ProjectInfo> Inspect();
}
