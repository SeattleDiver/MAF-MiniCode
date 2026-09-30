// Uncomment one line to switch this build to another provider. With neither,
// MiniCode talks to OpenAI.
// #define AZURE_OPENAI
// #define GEMINI

using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace MiniCode.Cli;

/// <summary>
/// The one place that knows which chat provider MiniCode talks to, and the only
/// type that reads provider settings from the environment. OpenAI unless this
/// file defines AZURE_OPENAI or GEMINI.
/// </summary>
internal static class ChatClientFactory
{
#if AZURE_OPENAI
    private const string ModelId = "gpt-4.1-mini"; // the Azure deployment name
    private const string ApiKeyVariable = "AZURE_OPENAI_API_KEY";
    private const string EndpointVariable = "AZURE_OPENAI_ENDPOINT";
#elif GEMINI
    private const string ModelId = "gemini-2.5-flash"; // Gemini 3.x rejects tool calls over this endpoint
    private const string ApiKeyVariable = "GEMINI_API_KEY";
#else
    private const string ModelId = "gpt-4.1-mini";
    private const string ApiKeyVariable = "OPENAI_API_KEY";
#endif

    /// <summary>Creates the configured provider's chat client, throwing if a setting is missing.</summary>
    public static IChatClient Create()
    {
        // Every provider here speaks the OpenAI wire protocol; only the base URL differs.
        var options = new OpenAIClientOptions();
#if AZURE_OPENAI
        options.Endpoint = new Uri(new Uri(Read(EndpointVariable)), "/openai/v1/");
#elif GEMINI
        options.Endpoint = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/");
#endif
        return new OpenAIClient(new ApiKeyCredential(Read(ApiKeyVariable)), options)
            .GetChatClient(ModelId)
            .AsIChatClient();
    }

    private static string Read(string variable) =>
        Environment.GetEnvironmentVariable(variable)
            ?? throw new InvalidOperationException($"{variable} is not set. Set it before running MiniCode.");
}
