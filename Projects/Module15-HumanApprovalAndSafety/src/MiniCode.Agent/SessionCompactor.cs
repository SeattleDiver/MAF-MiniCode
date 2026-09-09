using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace MiniCode.Agent;

/// <summary>
/// Replaces the older part of a session's history with one message describing
/// what it contained. Recent turns are kept verbatim: the model is usually
/// mid-thought, and a summary of the last exchange is worse than the exchange.
/// </summary>
public static class SessionCompactor
{
    /// <summary>How many recent messages are always kept exactly as they are.</summary>
    public const int PreservedMessages = 6;

    /// <summary>
    /// Compacts the session in place. Returns the estimated tokens reclaimed, or
    /// zero when there was nothing worth doing.
    /// </summary>
    public static int Compact(AgentSession session)
    {
        if (!session.TryGetInMemoryChatHistory(out List<ChatMessage>? history) || history is null)
        {
            return 0;
        }

        int cut = CutPoint(history);
        if (cut <= 0)
        {
            return 0;
        }

        WorkingState state = WorkingState.From(history.Take(cut));
        if (state.IsEmpty)
        {
            return 0;
        }

        int before = Estimate(history);
        List<ChatMessage> compacted = [new ChatMessage(ChatRole.User, state.Render()), .. history.Skip(cut)];
        session.SetInMemoryChatHistory(compacted);
        return before - Estimate(compacted);
    }

    // The cut lands on a user message, so a tool call is never separated from
    // the tool result that answers it.
    private static int CutPoint(IReadOnlyList<ChatMessage> history)
    {
        for (int i = history.Count - PreservedMessages; i > 0; i--)
        {
            if (history[i].Role == ChatRole.User)
            {
                return i;
            }
        }

        return 0;
    }

    private static int Estimate(IEnumerable<ChatMessage> messages) =>
        messages.Sum(m => TokenEstimator.Estimate(m.Text));
}
