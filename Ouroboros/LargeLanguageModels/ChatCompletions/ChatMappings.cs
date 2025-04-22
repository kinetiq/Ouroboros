using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Ouroboros.Extensions;
using System.Collections.Generic;

namespace Ouroboros.LargeLanguageModels.ChatCompletions;

internal class ChatMappings
{
    /// <summary>
    /// Maps our generic options to OpenAI options.
    /// </summary>
    internal static ChatCompletionCreateRequest MapOptions(List<ChatMessage> messages, ChatOptions options)
    {
        return new ChatCompletionCreateRequest
        {
            Messages = messages,
            Model = options.Model.GetModelNameAsString(),
            Temperature = options.Temperature,
            TopP = options.TopP,
            FrequencyPenalty = options.FrequencyPenalty,
            PresencePenalty = options.PresencePenalty,
            MaxTokens = options.MaxTokens,
            LogitBias = options.LogitBias,
            N = 1,
            Stop = options.Stop,
            StopAsList = options.StopAsList,
            User = options.User ?? string.Empty
        };
    }
}