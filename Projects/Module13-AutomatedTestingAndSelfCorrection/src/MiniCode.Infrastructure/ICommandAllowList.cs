namespace MiniCode.Infrastructure;

/// <summary>Decides which commands <c>run_command</c> is permitted to start.</summary>
public interface ICommandAllowList
{
    /// <summary>The allowed "program subcommand" pairs, for a message the model can read.</summary>
    IReadOnlyList<string> Entries { get; }

    /// <summary>True when <paramref name="command"/> plus its first argument is on the list.</summary>
    bool IsAllowed(string command, IReadOnlyList<string> arguments);
}
