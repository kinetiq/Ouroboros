using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels.RequestModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.LargeLanguageModels.Completions;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ouroboros.LargeLanguageModels;

internal class OpenAiClient 
{
    private readonly string ApiKey;

    /// <summary>
    /// Handles a text completion request.
    /// </summary>
    public async Task<CompleteResponseBase> CompleteAsync(string prompt, CompleteOptions? options)
    {
        var api = GetClient();
        var handler = new CompletionRequestHandler(api);

        return await handler.Complete(prompt, options);
    }

    /// <summary>
    /// Handles a chat completion request.
    /// </summary>
    public async Task<CompleteResponseBase> ChatAsync(List<ChatMessage> messages, ChatOptions? options = null)
    {
        var api = GetClient();
        var handler = new ChatRequestHandler(api);

        return await handler.CompleteAsync(messages, options);
    }

    internal OpenAIService GetClient()
    {
        return new OpenAIService(new OpenAiOptions
        {
            ApiKey = ApiKey
        });
    }

    internal OpenAiClient(string apiKey)
    {
        ApiKey = apiKey;
    }
}