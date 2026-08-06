using System;
using System.Collections.Generic;
using System.Linq;
using Anthropic.Models.Messages;
using AnthropicRole = Anthropic.Models.Messages.Role;
using Ouroboros.Core;
using Ouroboros.Extensions;
using Ouroboros.LargeLanguageModels.ChatCompletions;

namespace Ouroboros.LargeLanguageModels.Providers.Anthropic;

/// <summary>
/// Maps Ouroboros' request vocabulary onto Anthropic's.
/// </summary>
/// <remarks>
/// The sibling of ChatMappings, and duplicated from it on purpose: the two providers disagree about
/// enough that a shared mapper would be a pile of conditionals. The shapes that actually differ are
/// called out below.
/// </remarks>
internal static class AnthropicMappings
{
    internal static MessageCreateParams MapOptions(List<OuroMessage> messages, ChatOptions options)
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

        // Every member is init-only, so this has to be one initializer rather than built up.
        return new MessageCreateParams
        {
            Model = ModelMappings.GetModelNameAsString(model),

            // Required by Anthropic, unlike OpenAI where it is optional. Defaulting to the model's
            // own ceiling keeps behaviour closest to "no limit was asked for". It is a cap, not a
            // reservation - only tokens actually produced are billed. Note that on current models
            // this budget covers thinking as well as visible output, so a tight value truncates the
            // answer rather than merely shortening it.
            MaxTokens = options.MaxCompletionTokens ?? model.GetMaxOutputTokens(),

            Messages = MapMessages(messages, options.Attachments),

            System = system,

            StopSequences = options.StopSequences is { Count: > 0 }
                ? options.StopSequences.ToList()
                : null,

            Thinking = thinking,

            OutputConfig = effort is null ? null : new OutputConfig { Effort = effort },

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
        IReadOnlyList<OuroFileRef>? attachments)
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
