// -----------------------------------------------------------------------------
// MiniCode - the terminal front end
//
// Note what this project does NOT reference: no Microsoft.Agents.AI, no OpenAI
// SDK, no filesystem or shell types. One project reference, one contact point.
// -----------------------------------------------------------------------------

using MiniCode.Agent;

namespace MiniCode.Cli;

/// <summary>Entry point. It resolves an agent and hands it to the chat loop.</summary>
internal static class Program
{
    private static async Task<int> Main()
    {
        try
        {
            ICodingAgent agent = await CodingAgentFactory.CreateAsync();
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
