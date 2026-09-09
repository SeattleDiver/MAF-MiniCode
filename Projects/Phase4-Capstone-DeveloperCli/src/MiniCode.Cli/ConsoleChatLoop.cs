using MiniCode.Agent;

namespace MiniCode.Cli;

/// <summary>
/// The read-evaluate-print loop. Plain text still goes to the agent as a chat
/// turn; a line starting with "/" is dispatched as a command instead.
/// </summary>
internal sealed class ConsoleChatLoop(ICodingAgent agent)
{
    private const string HelpText = """
        /help    Show this list
        /status  Show the context budget
        /files   List files in the workspace
        /diff    Show unstaged changes
        /build   Run dotnet build
        /test    Run dotnet test
        /commit  Commit tracked changes, e.g. /commit fix the bug
        /plan    Plan a request without acting on it, e.g. /plan add caching
        /review  Review the current unstaged changes
        /clear   Discard the conversation so far
        /exit    Quit MiniCode
        """;

    private CancellationTokenSource _turnCancellation = new();

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _turnCancellation.Cancel();
        };

        Console.WriteLine("MiniCode. Type 'exit' to quit, or /help for commands.");

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("\n> ");
            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input) ||
                input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            _turnCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                if (SlashCommand.TryParse(input) is { } command)
                {
                    if (!await DispatchAsync(command, _turnCancellation.Token))
                    {
                        break;
                    }
                }
                else
                {
                    await RunChatTurnAsync(input, _turnCancellation.Token);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine();
                Console.WriteLine("Cancelled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }

    private async Task RunChatTurnAsync(string input, CancellationToken cancellationToken)
    {
        TaskPlan plan = await agent.PlanAsync(input, cancellationToken);
        if (!plan.IsEmpty)
        {
            Console.WriteLine(plan.Render());
            Console.WriteLine(Planner.NoChangesNotice);
            Console.WriteLine();
        }

        await foreach (string fragment in agent.RunStreamingAsync(input, cancellationToken))
        {
            Console.Write(fragment);
        }

        Console.WriteLine();
        Console.WriteLine(agent.ContextSummary);
    }

    /// <summary>Runs one command. Returns false only for /exit, to end the loop.</summary>
    private async Task<bool> DispatchAsync(SlashCommand command, CancellationToken cancellationToken)
    {
        switch (command.Name)
        {
            case "help":
                Console.WriteLine(HelpText);
                return true;
            case "status":
                Console.WriteLine(agent.ContextSummary);
                return true;
            case "files":
                Console.WriteLine(await agent.InvokeToolAsync("list_files", new Dictionary<string, object?>(), cancellationToken));
                return true;
            case "diff":
                Console.WriteLine(await agent.InvokeToolAsync("git_diff", new Dictionary<string, object?>(), cancellationToken));
                return true;
            case "build":
                Console.WriteLine(await RunShellAsync("dotnet", ["build"], cancellationToken));
                return true;
            case "test":
                Console.WriteLine(await RunShellAsync("dotnet", ["test"], cancellationToken));
                return true;
            case "commit":
                string message = string.IsNullOrWhiteSpace(command.Argument) ? "MiniCode commit" : command.Argument;
                Console.WriteLine(await RunShellAsync("git", ["commit", "-a", "-m", message], cancellationToken));
                return true;
            case "plan":
                if (string.IsNullOrWhiteSpace(command.Argument))
                {
                    Console.WriteLine("Usage: /plan <request>");
                    return true;
                }

                TaskPlan plan = await agent.PlanAsync(command.Argument, cancellationToken);
                Console.WriteLine(plan.IsEmpty ? "No plan produced." : plan.Render());
                return true;
            case "review":
                Console.WriteLine(await agent.ReviewAsync(cancellationToken));
                return true;
            case "clear":
                agent.ClearSession();
                Console.WriteLine("Session cleared.");
                return true;
            case "exit":
                return false;
            default:
                Console.WriteLine($"Unknown command: /{command.Name}. Type /help for a list.");
                return true;
        }
    }

    private Task<string> RunShellAsync(string command, string[] arguments, CancellationToken cancellationToken) =>
        agent.InvokeToolAsync(
            "run_command",
            new Dictionary<string, object?> { ["command"] = command, ["arguments"] = arguments },
            cancellationToken);
}
