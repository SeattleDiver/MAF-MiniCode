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

    private const string Instructions =
        "You are MiniCode, a concise assistant for software developers. You answer "
        + "questions about the repository you are working in, using tools rather than "
        + "guessing. Call describe_repository first on an unfamiliar solution, then "
        + "search_files to find where something lives, or list_files to see what is "
        + "there, then read_file on the few files that matter. Paths are always "
        + "relative to the workspace root. Cite files by path and line number. If a "
        + "tool refuses a call, read the message and correct the request rather than "
        + "retrying it unchanged.";

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
        var catalog = new ToolCatalog(workspace, files, inspector, new DirectToolInvoker());

        AIAgent agent = new ChatClientAgent(
            chatClient,
            Instructions,
            name: "MiniCode",
            tools: [.. catalog.GetTools()]);

        AgentSession session = await agent.CreateSessionAsync(cancellationToken);
        return new CodingAgent(agent, session, chatClient);
    }
}
