using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Anthropic.Models.Messages;
using AnthropicRole = Anthropic.Models.Messages.Role;
using Ouroboros.Core;
using Ouroboros.Extensions;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.StructuredOutput;

namespace Ouroboros.LargeLanguageModels.Providers.Anthropic;

/// <summary>
/// Maps Ouroboros' request vocabulary onto Anthropic's.
/// </summary>
/// <remarks>
/// The sibling of OpenAiMappings, and separate from it on purpose: the two providers disagree about
/// enough that a shared mapper would be a pile of conditionals. The shapes that actually differ are
/// called out below.
/// </remarks>
internal static class AnthropicMappings
{
    /// <summary>
    /// Builds a request from the conversation, optionally continuing a turn already under way.
    /// </summary>
    /// <param name="inProgress">
    /// Blocks the model has already produced for this turn, when continuing a paused one. They are
    /// appended as a single assistant turn - one that grows with each continuation rather than one
    /// per continuation, since consecutive assistant messages are not a shape the API takes.
    /// </param>
    /// <param name="maxTokens">
    /// Overrides the token ceiling for this request. Used when continuing a paused turn, where what
    /// is left of the caller's budget is smaller than the budget itself - max_tokens is per request,
    /// so passing the full ceiling each round would let one turn produce several times what the
    /// caller asked for.
    /// </param>
    internal static MessageCreateParams MapOptions(List<OuroMessage> messages, ChatOptions options,
        IReadOnlyList<ContentBlockParam>? inProgress = null, long? maxTokens = null)
    {
        // Demanded, not defaulted. Falling back to Constants.DefaultChatModel here was a latent
        // 404: that default is a GPT model, so an unresolved call would have stamped "gpt-5.4-mini"
        // onto an Anthropic request. Resolution belongs to ChatAsync and nowhere else.
        var model = options.Model ?? throw new InvalidOperationException(
            "ChatOptions.Model must be resolved before mapping. OuroClient.ChatAsync does this; a "
            + "provider reached with a null model means that step was bypassed.");

        // Anthropic carries the system prompt in its own top-level field rather than as a message
        // with a system role, so it has to be lifted out of the list.
        var joinedSystem = JoinSystemPrompts(messages);

        // Assigned through an explicit local rather than `cond ? joined : null` on purpose. These
        // SDK union types define an implicit conversion from string, and in a conditional the
        // compiler types the whole expression as string and converts the *result* - so the null
        // branch produces a non-null wrapper around a null value, and an empty system prompt gets
        // sent rather than omitted. The same trap applies to Thinking below.
        MessageCreateParamsSystem? system = null;

        if (!string.IsNullOrWhiteSpace(joinedSystem))
            system = joinedSystem;

        ThinkingConfigParam? thinking = null;

        // Adaptive thinking is the only supported mode on current models, and on some of them it is
        // on whether or not it is asked for. Requesting it explicitly keeps behaviour the same
        // across the family instead of varying by model.
        if (model.IsReasoningModel())
            thinking = new ThinkingConfigAdaptive();

        var effort = MapEffort(options.ReasoningEffort);
        var tools = MapServerTools(options.ServerTools);
        var format = MapSchema(options.ResponseType);

        // Every member is init-only, so this has to be one initializer rather than built up.
        return new MessageCreateParams
        {
            Model = ModelMappings.GetModelNameAsString(model),

            // Required by Anthropic, unlike OpenAI where it is optional. With no ceiling asked
            // for, the model's own is closest to "no limit". A cap, not a reservation: only tokens
            // actually produced are billed.
            //
            // The override comes first because a continuation gets what is left of the caller's
            // budget, not the whole of it again. See the maxTokens parameter.
            //
            // On current models this budget covers thinking as well as visible output, so a tight
            // value truncates the answer instead of shortening it.
            MaxTokens = maxTokens ?? options.MaxCompletionTokens ?? model.GetMaxOutputTokens(),

            Messages = MapMessages(messages, options.Attachments, inProgress),

            System = system,

            StopSequences = options.StopSequences is { Count: > 0 }
                ? options.StopSequences.ToList()
                : null,

            Thinking = thinking,

            OutputConfig = MapOutputConfig(effort, format),

            Tools = tools
        };
    }

    /// <summary>
    /// Turns the requested capabilities into Anthropic tool declarations.
    /// </summary>
    /// <remarks>
    /// The tool version lives here rather than in ChatOptions so callers never pin one. These
    /// identifiers move, and a caller who hard-coded last year's would be silently stuck on it.
    /// </remarks>
    private static List<ToolUnion>? MapServerTools(OuroServerTools serverTools)
    {
        if (serverTools == OuroServerTools.None)
            return null;

        var tools = new List<ToolUnion>();

        if (serverTools.HasFlag(OuroServerTools.CodeExecution))
            tools.Add(new CodeExecutionTool20260521());

        return tools.Count > 0 ? tools : null;
    }

    private static List<MessageParam> MapMessages(List<OuroMessage> messages,
        IReadOnlyList<OuroFileRef>? attachments, IReadOnlyList<ContentBlockParam>? inProgress = null)
    {
        var mapped = messages
            .Where(message => message.Role != OuroRole.System)
            .Select(message => new MessageParam
            {
                Role = message.Role == OuroRole.Assistant ? AnthropicRole.Assistant : AnthropicRole.User,
                Content = message.Content
            })
            .ToList();

        if (attachments is { Count: > 0 })
            AttachFiles(mapped, attachments);

        // After the attachments, so the files stay on the last user turn rather than landing on the
        // partial assistant one.
        if (inProgress is { Count: > 0 })
        {
            mapped.Add(new MessageParam
            {
                Role = AnthropicRole.Assistant,
                Content = inProgress.ToList()
            });
        }

        return mapped;
    }

    /// <summary>
    /// Hangs the uploaded files off the last user turn.
    /// </summary>
    /// <remarks>
    /// The last user turn specifically: that is the request the files are evidence for, and putting
    /// them there keeps them adjacent to the question in the model's context. A message's content
    /// has to become a block list to carry them, since the plain-string form has nowhere to put
    /// anything but text.
    /// </remarks>
    private static void AttachFiles(List<MessageParam> messages, IReadOnlyList<OuroFileRef> attachments)
    {
        var index = messages.FindLastIndex(message => (string?)message.Role == "user");

        var blocks = new List<ContentBlockParam>();

        // Preserve whatever the turn already said - the files are extra context for the question,
        // not a replacement for it.
        if (index >= 0 && messages[index].Content is { } existing && existing.TryPickString(out var text)
            && !string.IsNullOrEmpty(text))
        {
            blocks.Add(new TextBlockParam { Text = text });
        }

        foreach (var attachment in attachments)
            blocks.Add(new ContainerUploadBlockParam { FileID = attachment.Id });

        var withFiles = new MessageParam
        {
            Role = AnthropicRole.User,
            Content = blocks
        };

        if (index >= 0)
            messages[index] = withFiles;
        else
            messages.Add(withFiles);
    }

    /// <summary>
    /// Concatenates every system message into the single system prompt Anthropic accepts.
    /// </summary>
    /// <remarks>
    /// Ouroboros allows more than one, and dropping all but the first would silently lose
    /// instructions - a failure mode that shows up as the model ignoring a rule nobody can find.
    /// </remarks>
    private static string JoinSystemPrompts(List<OuroMessage> messages)
    {
        return string.Join(
            "\n\n",
            messages.Where(message => message.Role == OuroRole.System).Select(message => message.Content));
    }

    /// <summary>
    /// Builds the strict JSON-schema output format from the caller's type, or null when they asked
    /// for none.
    /// </summary>
    /// <remarks>
    /// The schema comes from Ouroboros' own generator, the same one the OpenAI mapper uses. That is
    /// what lets one ResponseType work on either provider, which is the whole point of generating it
    /// ourselves rather than taking a vendor's.
    ///
    /// Only the schema is supplied: the SDK writes the "json_schema" discriminator itself.
    /// </remarks>
    private static JsonOutputFormat? MapSchema(System.Type? responseType)
    {
        if (responseType is null)
            return null;

        // Through a string rather than converting the node tree by hand. This runs once per request
        // and is nothing beside the call it precedes, and hand-rolling JsonObject to JsonElement is
        // a lot of surface on which to get a nested case subtly wrong.
        var schema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            JsonSchemaGenerator.GenerateJson(responseType));

        return new JsonOutputFormat { Schema = schema! };
    }

    /// <summary>
    /// Combines the two things that ride on the output config, omitting it entirely when neither
    /// was asked for.
    /// </summary>
    /// <remarks>
    /// Spelled out as branches rather than one initializer with conditional values, because Effort
    /// is one of the SDK union wrappers with an implicit conversion. Assigning a null through it
    /// yields a non-null wrapper around nothing, which serialises as a present-but-empty field - the
    /// same trap already documented on System and Thinking above.
    /// </remarks>
    private static OutputConfig? MapOutputConfig(Effort? effort, JsonOutputFormat? format)
    {
        if (effort is null && format is null)
            return null;

        if (effort is null)
            return new OutputConfig { Format = format! };

        if (format is null)
            return new OutputConfig { Effort = effort };

        return new OutputConfig { Effort = effort, Format = format };
    }

    /// <summary>
    /// Maps our three-level effort onto Anthropic's.
    /// </summary>
    /// <remarks>
    /// Anthropic also offers xhigh and max above these. Our enum is a clean subset, so nothing is
    /// misreported - the extra headroom is simply not reachable yet.
    /// </remarks>
    private static Effort? MapEffort(OuroReasoningEffort? effort)
    {
        return effort switch
        {
            OuroReasoningEffort.Low => Effort.Low,
            OuroReasoningEffort.Medium => Effort.Medium,
            OuroReasoningEffort.High => Effort.High,
            null => null,
            _ => throw new ArgumentOutOfRangeException(nameof(effort), effort, "Unmapped reasoning effort.")
        };
    }
}
