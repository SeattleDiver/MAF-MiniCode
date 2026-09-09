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
