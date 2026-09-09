using MiniCode.Agent;

namespace MiniCode.Cli;

/// <summary>
/// The read-evaluate-print loop. It knows how to read a line and stream text
/// back, and nothing else — no model, no filesystem, no processes.
/// </summary>
internal sealed class ConsoleChatLoop(ICodingAgent agent)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("MiniCode. Type 'exit' to quit.");

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("\n> ");
            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input) ||
                input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            await foreach (string fragment in agent.RunStreamingAsync(input, cancellationToken))
            {
                Console.Write(fragment);
            }

            Console.WriteLine();
            Console.WriteLine(agent.ContextSummary);
        }
    }
}
