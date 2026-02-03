using Betalgo.Ranul.OpenAI.Contracts.Enums;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Microsoft.Extensions.Logging;
using Ouroboros.Extensions;
using System.Collections.Generic;

namespace Ouroboros.LargeLanguageModels.ChatCompletions;

internal class ChatMappings
{
    /// <summary>
    /// Maps our generic options to OpenAI options.
    /// </summary>
    internal static ChatCompletionCreateRequest MapOptions(List<ChatMessage> messages, ChatOptions options, ILogger? logger = null)
    {
        var reasoningEffort = options.ReasoningEffort;

        // For reasoning models, default to Medium since this is required.
        if (reasoningEffort == null && options.Model.HasValue && options.Model.Value.IsReasoningModel())
        {
            logger?.LogWarning("ReasoningEffort was not set for {Model}. Setting to Medium.", options.Model.Value);
            reasoningEffort = ReasoningEffort.Medium;
        }

        // Ignore ReasoningEffort for non-reasoning models.
        if (reasoningEffort != null && options.Model.HasValue && !options.Model.Value.IsReasoningModel())
        {
            logger?.LogWarning("ReasoningEffort was set to {ReasoningEffort} but model {Model} is not a reasoning model. Ignoring.", reasoningEffort, options.Model.Value);
            reasoningEffort = null;
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