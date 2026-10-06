using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace MiniCode.Cli;

/// <summary>
/// The one place that knows which chat provider MiniCode talks to, and the only
/// type that reads provider settings from the environment. MINI_CODE_LLM picks
/// the provider: OPENAI (the default when it is not set), AZURE, GEMINI or OLLAMA.
/// </summary>
internal static class ChatClientFactory
{
    private const string ProviderVariable = "MINI_CODE_LLM";

    /// <summary>Creates the configured provider's chat client, throwing if a setting is missing.</summary>
    public static IChatClient Create()
    {
        string provider = Environment.GetEnvironmentVariable(ProviderVariable) ?? "OPENAI";

        // Each case assigns the whole set. Anything still null after the switch
        // is a setting that could not be found.
        Uri? endpoint = null;
        string? modelId = null;
        string? apiKeyVariable = null;
        string? apiKey = null;

        // Every provider here speaks the OpenAI wire protocol; only these values differ.
        switch (provider.ToUpperInvariant())
        {
            case "OPENAI":
                modelId = "gpt-4.1-mini";
                endpoint = new Uri("https://api.openai.com/v1/");
                apiKeyVariable = "OPENAI_API_KEY";
                apiKey = Environment.GetEnvironmentVariable(apiKeyVariable);
                break;
            case "AZURE":
                modelId = "gpt-4.1-mini"; // the Azure deployment name
                endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") is string resource
                    ? new Uri(new Uri(resource), "/openai/v1/")
                    : null;
                apiKeyVariable = "AZURE_OPENAI_API_KEY";
                apiKey = Environment.GetEnvironmentVariable(apiKeyVariable);
                break;
            case "GEMINI":
                modelId = "gemini-2.5-flash"; // Gemini 3.x rejects tool calls over this endpoint
                endpoint = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/");
                apiKeyVariable = "GEMINI_API_KEY";
                apiKey = Environment.GetEnvironmentVariable(apiKeyVariable);
                break;
            case "OLLAMA":
                modelId = "qwen2.5-coder:7b";
                endpoint = new Uri("http://localhost:11434/v1/");
                apiKeyVariable = null;
                apiKey = "ollama"; // Ollama ignores the key, but the SDK refuses an empty one
                break;
        }

        // An unrecognised provider matched no case, so even modelId is still null.
        if (modelId is null)
        {
            throw new InvalidOperationException(
                $"{ProviderVariable} is '{provider}'. Set it to OPENAI, AZURE, GEMINI or OLLAMA.");
        }

        // Only Azure reads its endpoint from the environment.
        if (endpoint is null)
        {
            throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set. Set it before running MiniCode.");
        }

        if (apiKey is null)
        {
            throw new InvalidOperationException($"{apiKeyVariable} is not set. Set it before running MiniCode.");
        }

        return new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = endpoint })
            .GetChatClient(modelId)
            .AsIChatClient();
    }
}
