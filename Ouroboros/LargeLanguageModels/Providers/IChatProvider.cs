using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels.ChatCompletions;

namespace Ouroboros.LargeLanguageModels.Providers;

/// <summary>
/// One vendor's chat API, reduced to the single operation Ouroboros needs.
/// </summary>
/// <remarks>
/// Implementations make <b>exactly one attempt</b>. Retry, per-attempt timeout and cancellation all
/// belong to <see cref="ChatExecutor" />, so that policy is written once and behaves identically no
/// matter which provider served the call - rather than each provider growing its own subtly
/// different version of it.
///
/// Everything crossing this boundary is Ouroboros' own vocabulary. Provider wire types stop at the
/// implementation.
/// </remarks>
internal interface IChatProvider
{
    /// <summary>
    /// Which provider this is, for error messages and routing diagnostics.
    /// </summary>
    OuroProvider Kind { get; }

    /// <summary>
    /// Makes a single attempt against the provider.
    /// </summary>
    /// <remarks>
    /// Should not throw for ordinary provider failures - return them as a failed response so the
    /// executor can decide whether they are worth another try. Transport-level exceptions are fine
    /// to let escape; the executor handles those.
    /// </remarks>
    Task<ProviderAttempt> SendAsync(List<OuroMessage> messages, ChatOptions options,
        CancellationToken cancellationToken);
}
