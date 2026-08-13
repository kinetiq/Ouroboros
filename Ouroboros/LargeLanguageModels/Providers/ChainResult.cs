using System.Collections.Generic;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.Responses;

namespace Ouroboros.LargeLanguageModels.Providers;

/// <summary>
/// One entry of a fallback chain: a model, the provider that serves it, and the options to send.
/// </summary>
/// <remarks>
/// Options are per entry, not shared. A degraded entry carries a trimmed copy (see
/// ProviderCapabilities.Degrade), and each entry's Model must match the provider beside it or the
/// mapper stamps the wrong model id onto the request.
/// </remarks>
internal sealed record ChainEntry(OuroModels Model, IChatProvider Provider, ChatOptions Options);

/// <summary>
/// What one provider attempt produced.
/// </summary>
internal sealed record AttemptRecord(OuroModels Model, OuroResponseBase Response, int DurationMs);

/// <summary>
/// The outcome of running a fallback chain.
/// </summary>
/// <param name="Attempts">
/// Every attempt that completed, in order. More than one means the call failed over.
/// </param>
/// <param name="FinalResponse">
/// The response to hand the caller - the last attempt's, whether it succeeded or the chain ran out.
/// </param>
/// <param name="Cancelled">
/// Whether the caller's token was cancelled. Reported rather than thrown so the attempts above can
/// be logged first; the caller of the chain is responsible for actually throwing.
/// </param>
internal sealed record ChainResult(
    IReadOnlyList<AttemptRecord> Attempts,
    OuroResponseBase FinalResponse,
    bool Cancelled);

/// <summary>
/// One provider's outcome, plus whether the next provider in a chain deserves a go.
/// </summary>
internal sealed record ExecutionOutcome(OuroResponseBase Response, bool FailoverEligible);
