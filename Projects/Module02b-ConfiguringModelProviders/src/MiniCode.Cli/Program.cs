// -----------------------------------------------------------------------------
// MiniCode - the agent, with the provider behind a factory
//
// ChatClientFactory hands back an IChatClient. The ChatClientAgent around it,
// the AgentSession that remembers the conversation, and the streaming console
// loop are the same whichever provider built it. Module 3 splits these jobs
// across real projects.
// -----------------------------------------------------------------------------

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace MiniCode.Cli;

/// <summary>
/// Entry point for MiniCode. At this stage the agent is conversational only:
/// it has no filesystem access, no tools, and no shell.
/// </summary>
internal static class Program
{
    private static async Task Main()
    {
        // ChatClientFactory decides which provider this build talks to.
        IChatClient chatClient = ChatClientFactory.Create();

        // ChatClientAgent turns any IChatClient into a MAF agent. Swapping model
        // providers later means swapping only the IChatClient passed in here.
        AIAgent agent = new ChatClientAgent(
            chatClient,
            instructions: "You are MiniCode, a concise assistant for software developers.",
            name: "MiniCode");

        // The session carries conversation history across turns.
        AgentSession session = await agent.CreateSessionAsync();

        Console.WriteLine("MiniCode. Type 'exit' to quit.");

        while (true)
        {
            Console.Write("\n> ");
            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input) ||
                input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(input, session))
            {
                Console.Write(update);
            }

            Console.WriteLine();
        }
    }
}
