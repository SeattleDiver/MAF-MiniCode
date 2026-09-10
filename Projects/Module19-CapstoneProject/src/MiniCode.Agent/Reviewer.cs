using Microsoft.Extensions.AI;

namespace MiniCode.Agent;

/// <summary>
/// Reviews a Git diff for the things the syllabus names. Built the same way as
/// <see cref="Planner"/>: the call is made with no tools at all, so a review can
/// only read the diff it is handed — never the repository, never a file.
/// </summary>
public sealed class Reviewer(IChatClient client)
{
    private const string Prompt =
        "You are reviewing a Git diff, not a whole repository. Read only the diff below "
        + "and report, briefly: correctness problems, regressions, security issues, "
        + "changes that look unnecessary, and tests that appear to be missing. If the "
        + "diff raises none of these, say so plainly instead of inventing a concern.";

    /// <summary>Reviews the given diff. Empty input is reported without a model call.</summary>
    public async Task<string> ReviewAsync(string diff, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(diff))
        {
            return "Nothing to review — there are no unstaged changes.";
        }

        ChatResponse response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.System, Prompt), new ChatMessage(ChatRole.User, diff)],
            new ChatOptions { Tools = null, ToolMode = ChatToolMode.None },
            cancellationToken);

        return response.Text;
    }
}
