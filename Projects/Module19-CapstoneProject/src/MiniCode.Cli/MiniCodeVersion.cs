namespace MiniCode.Cli;

/// <summary>
/// The version MiniCode reports at startup. It moves at a Capstone and nowhere
/// else: 0.1 was the read-only Repository Explorer, 0.2 was the Planning Agent,
/// 0.3 was the Autonomous Fixer, 0.4 was the Developer CLI that closed Phase 4,
/// and 1.0 is the course capstone — the same code, declared finished. It lives
/// in the CLI because the banner is terminal output. The package version in
/// MiniCode.Cli.csproj is a separate literal that moves with this one by
/// discipline — nothing reads either from the other.
/// </summary>
internal static class MiniCodeVersion
{
    public const string Current = "1.0";
}
