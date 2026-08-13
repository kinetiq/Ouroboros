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
/// The sibling of AnthropicMappings. The two APIs agree on more than they disagree: both lift the
/// system prompt to a top-level field, both take an effort level, both express structured output as
/// a JSON schema. What differs is spelling, which is what a mapper is for.
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

        if (options.ServerTools.HasFlag(OuroServerTools.CodeExecution))
            AddCodeInterpreter(request, options);

        return request;
    }

    /// <summary>
    /// Turns on the code interpreter, mounting any attachments into its container.
    /// </summary>
    /// <remarks>
    /// The IncludedProperties line is not optional. Without it the request still succeeds and the
    /// tool still runs, but the outputs come back <em>empty</em>, so the model looks like it
    /// executed nothing. It opts in to a response field, not to the tool.
    ///
    /// The container stays automatic. OpenAI provisions one per response and disposes of it. An
    /// explicit container id would persist across calls, which is a different feature with a
    /// lifecycle to manage.
    /// </remarks>
    private static void AddCodeInterpreter(CreateResponseOptions request, ChatOptions options)
    {
        var fileIds = options.Attachments is { Count: > 0 } attachments
            ? attachments.Select(attachment => attachment.Id)
            : [];

        var container = new CodeInterpreterToolContainer(
            CodeInterpreterToolContainerConfiguration.CreateAutomaticContainerConfiguration(fileIds));

        request.Tools.Add(ResponseTool.CreateCodeInterpreterTool(container));
        request.IncludedProperties.Add(IncludedResponseProperty.CodeInterpreterCallOutputs);
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
    /// Ouroboros permits several. Dropping all but the first would lose instructions, which shows
    /// up later as the model ignoring a rule nobody can find.
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
    /// The casts on the arms are load-bearing. ResponseReasoningEffortLevel converts implicitly
    /// from string. Without them the compiler types the whole switch as the non-nullable level and
    /// runs the null arm through that conversion, throwing ArgumentNullException on every
    /// reasoning-model call that named no effort. Anthropic's wrappers carry the same trap.
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
