namespace MiniCode.Infrastructure;

/// <summary>
/// The fixed set run_command may start. Matched on the program and its first
/// argument together, so allowing "dotnet build" does not also allow
/// "dotnet nuget push" — every other dotnet subcommand stays refused.
/// </summary>
public sealed class CommandAllowList : ICommandAllowList
{
    // One entry per allowed subcommand — "dotnet" alone is deliberately absent,
    // so nothing else the model could put after it is allowed by accident.
    private static readonly string[] Allowed = ["dotnet restore", "dotnet build", "dotnet test", "dotnet format"];

    /// <inheritdoc />
    public IReadOnlyList<string> Entries => Allowed;

    /// <inheritdoc />
    public bool IsAllowed(string command, IReadOnlyList<string> arguments) =>
        arguments.Count > 0
        && Allowed.Contains($"{command} {arguments[0]}", StringComparer.OrdinalIgnoreCase);
}
