#nullable enable
using System;
using System.Collections.Generic;
using Ouroboros.Core;
using Ouroboros.Tracking;

namespace Ouroboros.LargeLanguageModels.ChatCompletions;
public class ChatOptions
{
    /// <summary>
    /// Name of the prompt for logging purposes. If not specified, will be inferred from the system message.
    /// </summary>
    public string? PromptName { get; set; }

    /// <summary>
    /// Session tracker for grouping related prompts across multiple dialogs/calls.
    /// </summary>
    public SessionTracker? Session { get; set; }

    /// <summary>
    /// Thread tracker for grouping prompts within a single conversation.
    /// </summary>
    public ThreadTracker? Thread { get; set; }

    /// <summary>
    /// Template variables that were used to render the prompt, if any. Flows through to
    /// ChatCompletedArgs so the OnChatCompleted hook can persist inputs alongside the chat.
    /// </summary>
    public Dictionary<string, string>? Variables { get; set; }

    /// <summary>
    ///     An upper bound for the number of tokens that can be generated for a completion,
    ///     including visible output tokens and reasoning tokens.
    /// </summary>
    /// <see href="https://platform.openai.com/docs/api-reference/chat/create#chat-create-max_completion_tokens" />
    public int? MaxCompletionTokens { get; set; }

    /// <summary>
    ///     Up to 4 sequences where the API will stop generating further tokens. The returned text will not contain the
    ///     stop sequence.
    /// </summary>
    /// <remarks>
    /// Honoured on Anthropic; refused on OpenAI, whose Responses API has no stop parameter at all.
    /// Shared rather than filed under <see cref="Anthropic" /> deliberately - the concept is
    /// universal and the gap is one API's, so relocating it would bake that gap into the public API
    /// and make undoing it a breaking change. Refusing loudly is the honest way to carry an
    /// asymmetry the caller cannot otherwise see.
    /// </remarks>
    public IList<string>? StopSequences { get; set; }

    public OuroModels? Model { get; set; }

    /// <summary>
    /// If set, used automatically for structured outputs: a JSON schema is generated from the type
    /// and the response is deserialized back into it. Null means no structured output.
    /// </summary>
    public Type? ResponseType { get; set; }

    /// <summary>
    ///     Constrains effort on reasoning for reasoning models. Reducing reasoning effort can result in
    ///     faster responses and fewer tokens used on reasoning in a response.
    /// </summary>
    public OuroReasoningEffort? ReasoningEffort { get; set; }

    public bool UseExponentialBackOff { get; set; }

    /// <summary>
    /// How long a single attempt may run before it is abandoned. Null uses
    /// <see cref="Constants.DefaultAttemptTimeout" />.
    /// </summary>
    /// <remarks>
    /// This is a per-attempt budget, not a ceiling on the whole call: with UseExponentialBackOff on,
    /// each retry gets a fresh one. To bound total latency, pass a CancellationToken to ChatAsync
    /// instead - the two compose, and they fail differently on purpose. A blown timeout comes back
    /// as a failure response; a cancelled token throws, per the usual .NET contract.
    ///
    /// The default is generous because reasoning models are slow and provider-side tools are slower
    /// still - a code-execution turn can run for minutes.
    /// </remarks>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Provider-side tools the model may use while answering - code execution, and so on.
    /// </summary>
    /// <remarks>
    /// Off by default: these cost money and change what the model can do, so they are opt-in per
    /// call. Requesting one from a provider that cannot serve it fails loudly rather than being
    /// quietly dropped, which would leave you reading a response that politely explains the model
    /// is unable to run code.
    /// </remarks>
    public OuroServerTools ServerTools { get; set; }

    /// <summary>
    /// Files to make available to the model, obtained from IOuroClient.UploadFileAsync.
    /// </summary>
    /// <remarks>
    /// Separate from the upload so one file can be referenced across many calls - re-uploading the
    /// same spreadsheet for every question about it would be absurd.
    ///
    /// Requires <see cref="ServerTools" /> to include CodeExecution: attachments are mounted into
    /// the execution container, so without one there is nowhere for them to go. Asking for them
    /// anyway fails rather than sending a request the model cannot act on.
    /// </remarks>
    public IReadOnlyList<OuroFileRef>? Attachments { get; set; }

    /// <summary>
    /// Settings that only mean something on OpenAI.
    /// </summary>
    /// <remarks>
    /// Never null, so callers can set into it without a null check. Each mapper reads only its own
    /// block, which is what makes it structurally impossible for a setting meant for one provider
    /// to reach the other - and lets both be populated on a single request, as a call able to run
    /// on either provider will need.
    /// </remarks>
    public OpenAiChatOptions OpenAi { get; init; } = new();

    /// <summary>
    /// Settings that only mean something on Anthropic. See <see cref="OpenAi" />.
    /// </summary>
    public AnthropicChatOptions Anthropic { get; init; } = new();

    /// <summary>
    /// A shallow copy, so the library can resolve defaults without writing into the caller's object.
    /// </summary>
    /// <remarks>
    /// ChatAsync used to assign Model and ReasoningEffort straight onto whatever instance it was
    /// handed. Reusing one ChatOptions across calls therefore baked the first call's defaults into
    /// it permanently, and a later SetDefaultChatModel silently did not apply.
    ///
    /// Shallow is right: the reference members are read-only inputs, and a caller who mutates a
    /// tracker mid-flight means it.
    ///
    /// Add a property, add it here - CloneCopiesEveryProperty in the test suite fails otherwise.
    /// </remarks>
    internal ChatOptions Clone()
    {
        return new ChatOptions
        {
            PromptName = PromptName,
            Session = Session,
            Thread = Thread,
            Variables = Variables,
            MaxCompletionTokens = MaxCompletionTokens,
            StopSequences = StopSequences,
            Model = Model,
            ResponseType = ResponseType,
            ReasoningEffort = ReasoningEffort,
            UseExponentialBackOff = UseExponentialBackOff,
            Timeout = Timeout,
            ServerTools = ServerTools,
            Attachments = Attachments,

            // Copied rather than shared. The rest of the reference members are read-only inputs, but
            // these are mutable settings the library may resolve defaults into - and writing a
            // default into the caller's object is precisely the bug Clone was added to fix.
            OpenAi = OpenAi.Clone(),
            Anthropic = Anthropic.Clone()
        };
    }

    public ChatOptions()
    {
        // Defaults
        MaxCompletionTokens = null;
        UseExponentialBackOff = true;
        ReasoningEffort = null;
        ResponseType = null;
        Timeout = null;
        ServerTools = OuroServerTools.None;
    }
}
