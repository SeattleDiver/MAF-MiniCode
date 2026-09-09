using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace MiniCode.Agent;

/// <summary>
/// The composition root: the one place that names OpenAI, reads the API key, and
/// assembles the agent. Everything above it depends on <see cref="ICodingAgent"/>.
/// </summary>
public static class CodingAgentFactory
{
    private const string ModelId = "gpt-4.1-mini";
    private const string ApiKeyVariable = "OPENAI_API_KEY";

    private const string Instructions =
        "You are MiniCode, a concise assistant for software developers.";

    /// <summary>Builds a ready-to-use agent, throwing if the API key is missing.</summary>
    public static async Task<ICodingAgent> CreateAsync(CancellationToken cancellationToken = default)
    {
        string apiKey = Environment.GetEnvironmentVariable(ApiKeyVariable)
            ?? throw new InvalidOperationException(
                $"{ApiKeyVariable} is not set. Set it before running MiniCode.");

        ChatClient openAiClient = new OpenAIClient(apiKey).GetChatClient(ModelId);
        IChatClient chatClient = openAiClient.AsIChatClient();

        AIAgent agent = new ChatClientAgent(chatClient, Instructions, name: "MiniCode");
        AgentSession session = await agent.CreateSessionAsync(cancellationToken);

        return new CodingAgent(agent, session, chatClient);
    }
}
