using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using MiniCode.Tools;
using OpenAI;
using OpenAI.Chat;

namespace MiniCode.Agent;

/// <summary>
/// The composition root: the one place that names OpenAI, reads the API key, and
/// assembles the agent with its tools.
/// </summary>
public static class CodingAgentFactory
{
    private const string ModelId = "gpt-4.1-mini";
    private const string ApiKeyVariable = "OPENAI_API_KEY";

    /// <summary>Builds a ready-to-use agent over the given workspace root.</summary>
    public static async Task<ICodingAgent> CreateAsync(
        string workspaceRoot,
        CancellationToken cancellationToken = default)
    {
        string apiKey = Environment.GetEnvironmentVariable(ApiKeyVariable)
            ?? throw new InvalidOperationException(
                $"{ApiKeyVariable} is not set. Set it before running MiniCode.");

        ChatClient openAiClient = new OpenAIClient(apiKey).GetChatClient(ModelId);
        IChatClient chatClient = openAiClient.AsIChatClient();

        var workspace = new MiniCode.Workspace.Workspace(workspaceRoot);
        var files = new MiniCode.Workspace.FileSystemService(workspace);
        var inspector = new MiniCode.Workspace.RepositoryInspector(workspace);
        var instructions = new MiniCode.Workspace.InstructionSource(workspace);
        var catalog = new ToolCatalog(workspace, files, inspector, new DirectToolInvoker());

        string composed = AgentInstructions.Compose(instructions.Load());

        AIAgent agent = new ChatClientAgent(
            chatClient,
            composed,
            name: "MiniCode",
            tools: [.. catalog.GetTools()]);

        AgentSession session = await agent.CreateSessionAsync(cancellationToken);
        return new CodingAgent(agent, session, chatClient, composed);
    }
}
