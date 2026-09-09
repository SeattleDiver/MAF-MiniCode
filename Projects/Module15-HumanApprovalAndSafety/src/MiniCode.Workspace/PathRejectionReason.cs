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
