using Polly.Contrib.WaitAndRetry;
using System;
using System.Collections.Generic;

namespace Ouroboros.LargeLanguageModels.Resilience;
internal static class BackoffPolicy
{
    /// <summary>
    /// Shared backoff policy for Polly. Can be disabled.
    /// </summary>
    public static IEnumerable<TimeSpan> GetBackoffPolicy(bool enabled)
    {
        // Five retries from a 5s base: roughly 5s, 10s, 20s, 40s, 80s - about 155s of waiting
        // across a fully exhausted attempt, before any time the calls themselves take. Worth
        // knowing when a fallback chain multiplies it by the number of entries.

        return Backoff.ExponentialBackoff(
            initialDelay: TimeSpan.FromSeconds(5),
            retryCount: enabled ? 5 : 0);
    }
}
