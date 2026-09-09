namespace MiniCode.Workspace;

/// <summary>
/// Finds the repository's own instructions to the agent. Discovery is filesystem
/// work, so it happens inside the sandbox — an AGENTS.md from outside the
/// workspace is not loadable, which matters because this text steers the model.
/// </summary>
public interface IInstructionSource
{
    /// <summary>
    /// The contents of the repository's <c>AGENTS.md</c>, or <c>null</c> when
    /// there is none. A missing file is the normal case, not an error.
    /// </summary>
    string? Load();
}
