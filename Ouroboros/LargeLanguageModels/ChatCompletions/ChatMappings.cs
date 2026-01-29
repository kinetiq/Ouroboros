using Betalgo.Ranul.OpenAI.Contracts.Enums;
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
        var reasoningEffort = options.ReasoningEffort;

        // For reasoning models, default to Low since this is required.
        if (reasoningEffort == null && options.Model.HasValue && options.Model.Value.IsReasoningModel())
        {
            reasoningEffort = ReasoningEffort.Low;
        }

        return new ChatCompletionCreateRequest
        {
            Messages = messages,
            Temperature = options.Temperature,
            TopP = options.TopP,
            FrequencyPenalty = options.FrequencyPenalty,
            PresencePenalty = options.PresencePenalty,
            MaxCompletionTokens = options.MaxCompletionTokens,
            LogitBias = options.LogitBias,
            N = 1,
            Stop = options.Stop,
            StopAsList = options.StopAsList,
            User = options.User ?? string.Empty,
            ResponseFormat = options.ResponseFormat,
            ReasoningEffort = reasoningEffort,
            Model = options.Model.GetModelNameAsString(Constants.DefaultChatModel)
        };
    }
}