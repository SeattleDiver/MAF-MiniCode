// -----------------------------------------------------------------------------
// MiniCode - a single file, on purpose
//
// An IChatClient, a ChatClientAgent wrapped around it, an AgentSession that
// remembers the conversation, and a streaming console loop. The next Module
// splits these four jobs across real projects.
// -----------------------------------------------------------------------------

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace MiniCode.Cli;

/// <summary>
/// Entry point for MiniCode. At this stage the agent is conversational only:
/// it has no filesystem access, no tools, and no shell.
/// </summary>
internal static class Program
{
    private const string ModelId = "gpt-4.1-mini";

    private static async Task Main()
    {
        string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? throw new InvalidOperationException(
                "OPENAI_API_KEY is not set. Set it before running MiniCode.");

        // Microsoft.Extensions.AI.OpenAI adapts the OpenAI SDK's ChatClient to
        // IChatClient, the provider-agnostic abstraction MAF builds on.
        ChatClient openAiClient = new OpenAIClient(apiKey).GetChatClient(ModelId);
        IChatClient chatClient = openAiClient.AsIChatClient();

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
