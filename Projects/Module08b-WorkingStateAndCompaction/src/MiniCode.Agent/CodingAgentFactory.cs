using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using MiniCode.Tools;

namespace MiniCode.Agent;

/// <summary>
/// The composition root: the one place that assembles the agent with its tools.
/// The model comes from <see cref="ChatClientFactory"/>, which alone knows the provider.
/// </summary>
public static class CodingAgentFactory
{
    /// <summary>Builds a ready-to-use agent over the given workspace root.</summary>
    public static async Task<ICodingAgent> CreateAsync(
        string workspaceRoot,
        CancellationToken cancellationToken = default)
    {
        IChatClient chatClient = ChatClientFactory.Create();

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
