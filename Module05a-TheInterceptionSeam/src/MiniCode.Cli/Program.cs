// -----------------------------------------------------------------------------
// MiniCode - the terminal front end
//
// The CLI resolves which directory MiniCode is allowed to work in and hands it
// to the agent layer. It still references no AI package and no tool type.
// -----------------------------------------------------------------------------

using MiniCode.Agent;

namespace MiniCode.Cli;

/// <summary>Entry point. It resolves the workspace, builds an agent, runs the loop.</summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        string root = Path.GetFullPath(args.Length > 0 ? args[0] : Directory.GetCurrentDirectory());

        if (!Directory.Exists(root))
        {
            Console.Error.WriteLine($"Workspace '{root}' does not exist.");
            return 1;
        }

        try
        {
            ICodingAgent agent = await CodingAgentFactory.CreateAsync(root);
            Console.WriteLine($"Workspace: {root}");
            await new ConsoleChatLoop(agent).RunAsync();
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
