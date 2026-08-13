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
    /// <remarks>
    /// Covers the whole turn, not each request within it. A paused Claude turn is continued
    /// automatically, and each continuation asks only for what is left of this. A turn taking four
    /// rounds still produces at most what was asked for here, not that much per round.
    ///
    /// A turn that spends the budget before finishing returns OuroStopReason.Paused.
    /// </remarks>
    /// <see href="https://platform.openai.com/docs/api-reference/chat/create#chat-create-max_completion_tokens" />
    public int? MaxCompletionTokens { get; set; }

    /// <summary>
    ///     Up to 4 sequences where the API will stop generating further tokens. The returned text will not contain the
    ///     stop sequence.
    /// </summary>
    /// <remarks>
    /// Honoured on Anthropic, refused on OpenAI, whose Responses API has no stop parameter at all.
    ///
    /// Deliberately shared rather than filed under <see cref="Anthropic" />. The concept is
    /// universal and the gap belongs to one API, so moving it here would bake that gap into the
    /// public API and make undoing it a breaking change.
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
    /// Never null, so you can set into it without a null check. Each mapper reads only its own
    /// block, so a setting meant for one provider cannot reach the other. Both can be populated at
    /// once, which is what a call able to run on either provider needs.
    /// </remarks>
    public OpenAiChatOptions OpenAi { get; init; } = new();

    /// <summary>
    /// Models to try, in order, if this call cannot be served on <see cref="Model" />.
    /// </summary>
    /// <remarks>
    /// Only transient failures move down the chain: rate limits and server faults that outlived the
    /// retry policy, transient network faults, a blown attempt timeout. A request the provider
    /// rejected on its merits fails where it stands. A bad parameter or an auth failure would fail
    /// the same way on the next provider, for twice the money.
    ///
    /// Each entry gets a full retry budget and its own per-attempt timeout, so a chain can run far
    /// longer than a single call. Bound it with a CancellationToken if that matters.
    ///
    /// Null means "use the client default" (OuroClient.SetDefaultFallback). An empty list means "no
    /// failover" and overrides that default - the same unset-versus-set distinction Model draws.
    /// </remarks>
    public IList<OuroModels>? FallbackModels { get; set; }

    /// <summary>
    /// Whether a fallback may serve this call without an option the primary could honour.
    /// </summary>
    /// <remarks>
    /// Off by default. A chain that cannot carry every option fails when the call is made, naming
    /// the option, instead of quietly returning output shaped differently from what was asked for.
    /// Turning this on says a less exact answer beats no answer.
    ///
    /// It never drops an attachment. A model reasoning about a file it cannot see answers a
    /// different question confidently, so a provider that cannot see this call's files is skipped
    /// instead. In practice this governs StopSequences, which Responses cannot express.
    ///
    /// Only consulted when a fallback chain exists. A single-model call behaves exactly as before.
    /// </remarks>
    public bool AllowDegraded { get; set; }

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
            FallbackModels = FallbackModels,
            AllowDegraded = AllowDegraded,

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
