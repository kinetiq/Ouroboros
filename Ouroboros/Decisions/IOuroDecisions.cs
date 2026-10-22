using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Ouroboros.Responses;

namespace Ouroboros.Decisions;

/// <summary>
/// Evaluates evidence against predicates, choices and scores, and returns typed answers with
/// probabilities. Served by OpenAI's Decisions API.
/// </summary>
/// <remarks>
/// Separate from IOuroClient so that adding it breaks nobody who implements IOuroClient, and so the
/// experimental marker stays on this surface alone. AddOuroboros registers both against the same
/// client.
///
/// OpenAI only, with no fallback chain: no other provider has an equivalent API. Calling it on a
/// client with no OpenAI key throws, as ChatAsync does for a model whose provider has no key.
/// </remarks>
[Experimental(DecisionsDiagnostics.ExperimentalId)]
public interface IOuroDecisions
{
    /// <summary>
    /// Answers every question in the request against the same evidence, in one call.
    /// </summary>
    /// <returns>
    /// <see cref="OuroDecisionResponse" /> on success. On failure, the OuroResponseProviderError or
    /// OuroResponseInternalError that ChatAsync also returns. A request that cannot work is refused
    /// before any call is made.
    /// </returns>
    /// <param name="cancellationToken">
    /// Bounds the whole call, retries included. Cancelling throws OperationCanceledException, as on
    /// ChatAsync. For a per-attempt budget, see DecisionOptions.Timeout.
    /// </param>
    Task<OuroResponseBase> DecideAsync(OuroDecisionRequest request, DecisionOptions? options = null,
        CancellationToken cancellationToken = default);
}
