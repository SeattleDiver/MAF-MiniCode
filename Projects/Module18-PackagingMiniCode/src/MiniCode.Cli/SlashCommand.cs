namespace MiniCode.Cli;

/// <summary>One "/name rest of line" command, parsed from what the operator typed.</summary>
public sealed record SlashCommand(string Name, string? Argument)
{
    /// <summary>Parses input starting with "/", or null when it does not.</summary>
    public static SlashCommand? TryParse(string input)
    {
        if (!input.StartsWith('/'))
        {
            return null;
        }

        string body = input[1..].Trim();
        int space = body.IndexOf(' ');
        return space < 0
            ? new SlashCommand(body.ToLowerInvariant(), null)
            : new SlashCommand(body[..space].ToLowerInvariant(), body[(space + 1)..].Trim());
    }
}
