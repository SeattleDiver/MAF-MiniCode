namespace MiniCode.Infrastructure;

/// <summary>
/// What running a command produced. <see cref="ExitCode"/> is <c>null</c>
/// exactly when <see cref="TimedOut"/> is <c>true</c> — a killed process never
/// exits, so it never has one.
/// </summary>
public sealed record ShellCommandResult(int? ExitCode, string StandardOutput, string StandardError, bool TimedOut);
