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
        return Backoff.ExponentialBackoff(
            initialDelay: TimeSpan.FromSeconds(2),
            retryCount: enabled ? 3 : 0);
    }
}
