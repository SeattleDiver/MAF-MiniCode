using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace MiniCode.Agent;

/// <summary>
/// The composition root: the one place that assembles the agent. The model comes
/// from <see cref="ChatClientFactory"/>; everything above depends on <see cref="ICodingAgent"/>.
/// </summary>
public static class CodingAgentFactory
{
    private const string Instructions =
        "You are MiniCode, a concise assistant for software developers.";

    /// <summary>Builds a ready-to-use agent, throwing if the API key is missing.</summary>
    public static async Task<ICodingAgent> CreateAsync(CancellationToken cancellationToken = default)
    {
        IChatClient chatClient = ChatClientFactory.Create();

        AIAgent agent = new ChatClientAgent(chatClient, Instructions, name: "MiniCode");
        AgentSession session = await agent.CreateSessionAsync(cancellationToken);

        return new CodingAgent(agent, session, chatClient);
    }
}
