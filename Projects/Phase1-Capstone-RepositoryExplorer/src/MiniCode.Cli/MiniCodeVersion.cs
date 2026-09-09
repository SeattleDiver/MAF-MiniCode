namespace MiniCode.Cli;

/// <summary>
/// The version MiniCode reports at startup. It moves at a Phase Capstone and
/// nowhere else: 0.1 is the read-only Repository Explorer that closes Phase 1.
/// It lives in the CLI because versioning is a packaging concern, and packaging
/// is where Module 18 picks this constant up.
/// </summary>
internal static class MiniCodeVersion
{
    public const string Current = "0.1";
}
