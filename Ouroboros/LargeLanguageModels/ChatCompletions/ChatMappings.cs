using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Microsoft.Extensions.Logging;
using Ouroboros.Extensions;
using Ouroboros.Core;
using Ouroboros.StructuredOutput;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ouroboros.LargeLanguageModels.ChatCompletions;

internal class ChatMappings
{
    /// <summary>
    /// Maps our generic options to OpenAI options.
    /// </summary>
    internal static ChatCompletionCreateRequest MapOptions(List<OuroMessage> messages, ChatOptions options, ILogger? logger = null)
    {
        var reasoningEffort = options.ReasoningEffort;

        // For reasoning models, default to Medium since this is required.
        if (reasoningEffort == null && options.Model.HasValue && options.Model.Value.IsReasoningModel())
        {
            logger?.LogWarning("ReasoningEffort was not set for {Model}. Setting to Medium.", options.Model.Value);
            reasoningEffort = OuroReasoningEffort.Medium;
        }

        // Ignore ReasoningEffort for non-reasoning models.
        if (reasoningEffort != null && options.Model.HasValue && !options.Model.Value.IsReasoningModel())
        {
            logger?.LogWarning("ReasoningEffort was set to {ReasoningEffort} but model {Model} is not a reasoning model. Ignoring.", reasoningEffort, options.Model.Value);
            reasoningEffort = null;
        }

        return new ChatCompletionCreateRequest
        {
            Messages = messages.Select(x => x.ToBetalgo()).ToList(),
            MaxCompletionTokens = options.MaxCompletionTokens,
            N = 1,
            StopAsList = options.StopSequences,
            User = options.User ?? string.Empty,
            // Built here rather than on the caller's ChatOptions, so reusing one options
            // instance across calls with different ResponseTypes can't leave a stale schema.
            ResponseFormat = options.ResponseType is not null ? Json.GetSchema(options.ResponseType) : null,
            ReasoningEffort = reasoningEffort.ToBetalgo(),
            // Demanded, not defaulted - resolution belongs to ChatAsync and nowhere else. A second
            // fallback here would quietly disagree with the model reported to the OnChatCompleted
            // hook, and token counts would then be taken against the wrong tokenizer.
            Model = ModelMappings.GetModelNameAsString(
                options.Model ?? throw new InvalidOperationException(
                    "ChatOptions.Model must be resolved before mapping. OuroClient.ChatAsync does "
                    + "this; a provider reached with a null model means that step was bypassed."))
        };
    }
}