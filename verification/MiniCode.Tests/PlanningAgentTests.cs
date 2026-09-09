using Microsoft.Extensions.AI;
using MiniCode.Agent;
using Xunit;

namespace MiniCode.Tests;

/// <summary>Verifies the two Phase 2 seams. Ours, not the course's.</summary>
public sealed class PlanningAgentTests
{
    private sealed class CapturingClient : IChatClient
    {
        public IList<ChatMessage>? Seen { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            Seen = [.. messages];
            Options = options;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "1. Do the thing")));
        }

        public ChatOptions? Options { get; private set; }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    [Fact]
    public async Task ThePlannerIsToldTheRepositoryConventions()
    {
        var client = new CapturingClient();
        await new Planner(client, "Rules:\n- Add tests for new functionality.").CreateAsync("add caching");

        string system = client.Seen!.First(m => m.Role == ChatRole.System).Text;
        Assert.Contains("Add tests for new functionality", system);
        Assert.Contains("A plan that ignores them is wrong", system);
    }

    [Fact]
    public async Task APlannerWithNoConventionsSendsThePromptAlone()
    {
        var client = new CapturingClient();
        await new Planner(client).CreateAsync("add caching");

        Assert.DoesNotContain("conventions", client.Seen!.First(m => m.Role == ChatRole.System).Text);
    }

    [Fact]
    public async Task PlanningNeverOffersTools()
    {
        var client = new CapturingClient();
        await new Planner(client, "some conventions").CreateAsync("add caching");

        Assert.Null(client.Options!.Tools);
        Assert.Equal(ChatToolMode.None, client.Options.ToolMode);
    }
}
