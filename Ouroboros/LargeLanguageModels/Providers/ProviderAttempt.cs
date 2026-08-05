using Ouroboros.Responses;

namespace Ouroboros.LargeLanguageModels.Providers;

/// <summary>
/// The outcome of one attempt against a provider, plus whether it is worth trying again.
/// </summary>
/// <remarks>
/// The retryable flag is what lets the retry policy stay provider-neutral. Each provider knows what
/// its own 429s and 503s look like and says so here; the executor never has to learn a second
/// vendor's error vocabulary to decide whether to back off.
/// </remarks>
internal sealed record ProviderAttempt(OuroResponseBase Response, bool IsRetryable)
{
    /// <summary>
    /// A result to return as-is - success, or a failure that retrying will not improve.
    /// </summary>
    public static ProviderAttempt Final(OuroResponseBase response) => new(response, false);

    /// <summary>
    /// A failure worth another attempt: rate limiting, an overloaded provider, a transient 5xx.
    /// </summary>
    public static ProviderAttempt Retryable(OuroResponseBase response) => new(response, true);
}
