using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace MiniCode.Agent;

/// <summary>
/// Turns a request into a plan. The call is made with no tools at all, so
/// planning cannot read, write or run anything — it can only think.
/// </summary>
public sealed partial class Planner(IChatClient client)
{
    private const string Prompt =
        "Break the developer's request into at most seven short steps, one per line, "
        + "numbered '1.' onward. Each step is something you would actually do to a .NET "
        + "repository — inspect, locate, change, build, test, review. No preamble, no "
        + "commentary, no sub-steps. If the request is too vague to plan, reply with a "
        + "single step asking the one question that would unblock it.";

    /// <summary>The line MiniCode prints under every plan.</summary>
    public const string NoChangesNotice = "No files have been modified.";

    /// <summary>Produces a plan without touching the repository.</summary>
    public async Task<TaskPlan> CreateAsync(string request, CancellationToken cancellationToken = default)
    {
        ChatResponse response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.System, Prompt), new ChatMessage(ChatRole.User, request)],
            new ChatOptions { Tools = null, ToolMode = ChatToolMode.None },
            cancellationToken);

        return Parse(request, response.Text);
    }

    /// <summary>Reads numbered lines into steps, ignoring anything else the model said.</summary>
    public static TaskPlan Parse(string goal, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return TaskPlan.None;
        }

        int id = 1;
        List<PlanTask> tasks = [.. text.Split('\n')
            .Select(line => StepLine().Match(line.Trim()))
            .Where(m => m.Success)
            .Select(m => new PlanTask(id++, m.Groups["title"].Value.Trim(), TaskState.Pending))];

        return tasks.Count == 0 ? TaskPlan.None : new TaskPlan(goal, tasks);
    }

    [GeneratedRegex(@"^\d+[.)]\s+(?<title>.+)$")]
    private static partial Regex StepLine();
}
