using System;
using System.Collections.Generic;
using System.Linq;
using OpenAI.Responses;
using Ouroboros.Core;
using Ouroboros.Extensions;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.StructuredOutput;

namespace Ouroboros.LargeLanguageModels.Providers.OpenAi;

/// <summary>
/// Maps Ouroboros' request vocabulary onto OpenAI's Responses API.
/// </summary>
/// <remarks>
/// The sibling of AnthropicMappings. The two APIs turn out to agree on more than they disagree:
/// both lift the system prompt to a top-level field, both take an effort level, and both express
/// structured output as a JSON schema. What differs is spelling, which is what a mapper is for.
/// </remarks>
internal static class OpenAiMappings
{
    internal static CreateResponseOptions MapOptions(List<OuroMessage> messages, ChatOptions options)
    {
        // Demanded, not defaulted - resolution belongs to ChatAsync and nowhere else.
        var model = options.Model ?? throw new InvalidOperationException(
            "ChatOptions.Model must be resolved before mapping. OuroClient.ChatAsync does this; a "
            + "provider reached with a null model means that step was bypassed.");

        var request = new CreateResponseOptions
        {
            Model = ModelMappings.GetModelNameAsString(model),
            MaxOutputTokenCount = options.MaxCompletionTokens,
            EndUserId = options.OpenAi.User
        };

        // Like Anthropic, the Responses API carries the system prompt in its own field rather than
        // as a message in the list, so it has to be lifted out.
        var system = JoinSystemPrompts(messages);

        if (!string.IsNullOrWhiteSpace(system))
            request.Instructions = system;

        foreach (var item in MapMessages(messages))
            request.InputItems.Add(item);

        if (model.IsReasoningModel() && MapEffort(options.ReasoningEffort) is { } effort)
            request.ReasoningOptions = new ResponseReasoningOptions { ReasoningEffortLevel = effort };

        if (options.ResponseType is { } responseType)
            request.TextOptions = new ResponseTextOptions { TextFormat = MapSchema(responseType) };

        return request;
    }

    private static IEnumerable<ResponseItem> MapMessages(List<OuroMessage> messages)
    {
        return messages
            .Where(message => message.Role != OuroRole.System)
            .Select(message => message.Role == OuroRole.Assistant
                ? ResponseItem.CreateAssistantMessageItem(message.Content)
                : ResponseItem.CreateUserMessageItem(message.Content));
    }

    /// <summary>
    /// Concatenates every system message into the single instructions field.
    /// </summary>
    /// <remarks>
    /// Ouroboros permits several; dropping all but the first would silently lose instructions,
    /// which surfaces later as the model ignoring a rule nobody can find.
    /// </remarks>
    private static string JoinSystemPrompts(List<OuroMessage> messages)
    {
        return string.Join(
            "\n\n",
            messages.Where(message => message.Role == OuroRole.System).Select(message => message.Content));
    }

    /// <summary>
    /// Builds the strict JSON-schema output format from the caller's type.
    /// </summary>
    /// <remarks>
    /// The schema comes from Ouroboros' own generator rather than anything SDK-shaped - that is
    /// what lets the same ResponseType work on more than one provider.
    /// </remarks>
    private static ResponseTextFormat MapSchema(Type responseType)
    {
        return ResponseTextFormat.CreateJsonSchemaFormat(
            responseType.Name,
            BinaryData.FromString(JsonSchemaGenerator.GenerateJson(responseType)),
            jsonSchemaIsStrict: true);
    }

    /// <summary>
    /// Maps our three-level effort onto OpenAI's.
    /// </summary>
    /// <remarks>
    /// The API also offers None and Minimal below Low. Our enum is a subset, so nothing is
    /// misreported - the extra levels are simply not reachable yet.
    ///
    /// The casts on the arms are load-bearing. ResponseReasoningEffortLevel converts implicitly
    /// from string, so without them the compiler types the whole switch as the non-nullable level
    /// and runs the null arm through that conversion - which throws ArgumentNullException on every
    /// reasoning-model call that did not specify an effort. Anthropic's wrappers have the same
    /// shape and the same trap.
    /// </remarks>
    private static ResponseReasoningEffortLevel? MapEffort(OuroReasoningEffort? effort)
    {
        return effort switch
        {
            OuroReasoningEffort.Low => (ResponseReasoningEffortLevel?)ResponseReasoningEffortLevel.Low,
            OuroReasoningEffort.Medium => (ResponseReasoningEffortLevel?)ResponseReasoningEffortLevel.Medium,
            OuroReasoningEffort.High => (ResponseReasoningEffortLevel?)ResponseReasoningEffortLevel.High,
            null => null,
            _ => throw new ArgumentOutOfRangeException(nameof(effort), effort, "Unmapped reasoning effort.")
        };
    }
}
