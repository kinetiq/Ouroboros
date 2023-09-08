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
        // The delay schedule should be: 5s, 10s, 20s, 40s = 75s total.

        return Backoff.ExponentialBackoff(
            initialDelay: TimeSpan.FromSeconds(5),
            retryCount: enabled ? 5 : 0);
    }
}
