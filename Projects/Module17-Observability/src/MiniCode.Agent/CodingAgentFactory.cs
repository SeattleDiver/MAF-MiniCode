using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
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
        IApprovalPrompter prompter,
        CancellationToken cancellationToken = default)
    {
        string apiKey = Environment.GetEnvironmentVariable(ApiKeyVariable)
            ?? throw new InvalidOperationException(
                $"{ApiKeyVariable} is not set. Set it before running MiniCode.");

        // The composition root builds this, not MiniCode.Cli — the logging
        // provider lives in MiniCode.Infrastructure, which Cli cannot reference.
        // CodingAgent owns disposing it, alongside the chat client.
        ILoggerFactory loggerFactory = LoggerFactory.Create(
            builder => builder.AddProvider(new MiniCode.Infrastructure.BracketLoggerProvider()));

        ChatClient openAiClient = new OpenAIClient(apiKey).GetChatClient(ModelId);
        IChatClient chatClient = openAiClient.AsIChatClient();

        var workspace = new MiniCode.Workspace.Workspace(workspaceRoot);
        var files = new MiniCode.Workspace.FileSystemService(workspace);
        var inspector = new MiniCode.Workspace.RepositoryInspector(workspace);
        var instructions = new MiniCode.Workspace.InstructionSource(workspace);
        var shell = new MiniCode.Infrastructure.AllowListedShellExecutor(
            new MiniCode.Infrastructure.ShellExecutor(), new MiniCode.Infrastructure.CommandAllowList());
        var git = new MiniCode.Infrastructure.GitClient(new MiniCode.Infrastructure.ShellExecutor());
        var tracing = new TracingInvoker(new DirectToolInvoker(), loggerFactory);
        var approval = new ApprovalInvoker(tracing, prompter);
        var guard = new LoopGuardInvoker(approval);
        var catalog = new ToolCatalog(workspace, files, inspector, shell, git, guard);

        string composed = AgentInstructions.Compose(instructions.Load());
        IReadOnlyList<AITool> tools = catalog.GetTools();

        AIAgent agent = new ChatClientAgent(
            chatClient, composed, name: "MiniCode", tools: [.. tools], loggerFactory: loggerFactory);

        AgentSession session = await agent.CreateSessionAsync(cancellationToken);
        return new CodingAgent(
            agent, session, chatClient, composed,
            new Planner(chatClient, instructions.Load()), new Reviewer(chatClient), guard, tools, loggerFactory);
    }
}
