namespace MiniCode.Agent;

/// <summary>
/// The one thing the terminal is allowed to know about the agent. Deliberately
/// expressed in <see cref="string"/> only, so no framework type crosses into
/// <c>MiniCode.Cli</c>.
/// </summary>
public interface ICodingAgent
{
    /// <summary>Streams the agent's answer to a request, fragment by fragment.</summary>
    IAsyncEnumerable<string> RunStreamingAsync(
        string request,
        CancellationToken cancellationToken = default);
}
