using System.Collections.Generic;
using System.Linq;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels.ChatCompletions;

namespace Ouroboros.LargeLanguageModels.Providers;

/// <summary>
/// How completely a provider can honour a set of options.
/// </summary>
internal enum CapabilityVerdict
{
    /// <summary>
    /// Everything set on the request can be served as asked.
    /// </summary>
    Supported,

    /// <summary>
    /// One option cannot be served, but dropping it still answers the question that was asked.
    /// Only ever acted on when the caller opts in with <see cref="ChatOptions.AllowDegraded" />.
    /// </summary>
    Degradable,

    /// <summary>
    /// This provider cannot serve the call at all, though another one could.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="Degradable" />. Dropping the offending option here would answer a
    /// different question: a model reasoning about a file it cannot see. That comes back looking
    /// like a confident success.
    /// </remarks>
    NotServable,

    /// <summary>
    /// The request is malformed regardless of who serves it. No provider will do, and failing over
    /// would only spend money confirming it.
    /// </summary>
    Invalid
}

/// <summary>
/// The outcome of a capability check, with the message a caller needs to fix it.
/// </summary>
internal sealed record CapabilityCheck(CapabilityVerdict Verdict, string? Message)
{
    public static readonly CapabilityCheck Supported = new(CapabilityVerdict.Supported, null);

    public bool IsSupported => Verdict == CapabilityVerdict.Supported;
}

/// <summary>
/// What each provider can and cannot honour, in one place.
/// </summary>
/// <remarks>
/// Two callers must never disagree about this: a provider's own pre-flight refusal, and the
/// failover chain's up-front validation. If the chain thought a provider could serve a call the
/// provider then refused, failover would burn an attempt learning what was already known.
///
/// Everything here is either an Ouroboros rule or a vendor's hard gap, never a preference. A rule
/// that held on one provider by accident would be a mapper bug, not an entry here.
/// </remarks>
internal static class ProviderCapabilities
{
    /// <summary>
    /// The most severe problem this provider has with this request.
    /// </summary>
    public static CapabilityCheck Check(List<OuroMessage> messages, ChatOptions options, OuroProvider provider)
    {
        // Invalid first: wrong whoever serves it, so it must not read as "try the next one".
        //
        // An empty conversation asks nothing. Both providers reject it, but each with its own
        // opaque wire error - OpenAI complains about a missing 'input', which names its request
        // field rather than the caller's actual mistake.
        if (messages.Count == 0)
        {
            return new CapabilityCheck(CapabilityVerdict.Invalid,
                "At least one message is required. The message list is empty.");
        }

        // Anthropic has no system role inside the messages array - system prompts ride in a
        // top-level parameter - so a system-only call leaves messages empty, and the API requires
        // at least one. NotServable rather than Invalid: OpenAI can express the same call as
        // system-role input items.
        if (provider == OuroProvider.Anthropic && messages.All(message => message.Role == OuroRole.System))
        {
            return new CapabilityCheck(CapabilityVerdict.NotServable,
                "Claude requires at least one user or assistant message; a system-only conversation "
                + "cannot be expressed in its API. Add a user message, or route the call to an "
                + "OpenAI model.");
        }

        // Attachments mount into the execution container, so without that tool there is nowhere for
        // them to go. Sent anyway they are accepted and ignored, and the model answers as though the
        // file were never mentioned - which reads as the model being obtuse.
        if (options.Attachments is { Count: > 0 } && !options.ServerTools.HasFlag(OuroServerTools.CodeExecution))
        {
            return new CapabilityCheck(CapabilityVerdict.Invalid,
                "Attachments were supplied without OuroServerTools.CodeExecution. Files are mounted "
                + "into the execution container, so without one there is nowhere for them to go.");
        }

        // File ids are opaque and issued per provider. Handing one to the wrong vendor comes back as
        // a 404 about an id the caller can plainly see exists.
        if (options.Attachments is { Count: > 0 } attachments)
        {
            foreach (var attachment in attachments)
            {
                if (attachment.Provider != provider)
                {
                    return new CapabilityCheck(CapabilityVerdict.NotServable,
                        $"Attachment '{attachment.Id}' belongs to {attachment.Provider}, but this call "
                        + $"routes to {provider}. File references are not portable between providers - "
                        + "upload the file to the one serving the call.");
                }
            }
        }

        // The Responses API has no stop parameter at all - an API-level gap, not an SDK omission.
        // Degradable rather than fatal: an answer that runs past where it was asked to stop is still
        // an answer to the question, which is a judgement only the caller can make.
        if (provider == OuroProvider.OpenAi && options.StopSequences is { Count: > 0 })
        {
            return new CapabilityCheck(CapabilityVerdict.Degradable,
                "ChatOptions.StopSequences is not supported on OpenAI's Responses API, which has no "
                + "stop parameter. Remove them, or route the call to a Claude model.");
        }

        return CapabilityCheck.Supported;
    }

    /// <summary>
    /// A copy of these options with anything this provider cannot honour removed.
    /// </summary>
    /// <remarks>
    /// Reached only under <see cref="ChatOptions.AllowDegraded" />, and only for a verdict of
    /// <see cref="CapabilityVerdict.Degradable" />. The caller has said outright that a less exact
    /// answer beats no answer. Nothing here drops an attachment - see
    /// <see cref="CapabilityVerdict.NotServable" />.
    /// </remarks>
    public static ChatOptions Degrade(ChatOptions options, OuroProvider provider)
    {
        var degraded = options.Clone();

        if (provider == OuroProvider.OpenAi)
            degraded.StopSequences = null;

        return degraded;
    }
}
