namespace MiniCode.Cli;

/// <summary>
/// The version MiniCode reports at startup. It moves at a Phase Capstone and
/// nowhere else: 0.1 was the read-only Repository Explorer, 0.2 was the Planning
/// Agent, 0.3 was the Autonomous Fixer, 0.4 is the Developer CLI that closes
/// Phase 4. It lives in the CLI because the banner is terminal output. The
/// package version in MiniCode.Cli.csproj is a separate literal that moves with
/// this one by discipline — nothing reads either from the other.
/// </summary>
internal static class MiniCodeVersion
{
    public const string Current = "0.4";
}
