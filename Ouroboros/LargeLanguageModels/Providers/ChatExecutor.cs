using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.LargeLanguageModels.Resilience;
using Ouroboros.Responses;
using Polly;

namespace Ouroboros.LargeLanguageModels.Providers;

/// <summary>
/// Runs a provider attempt under Ouroboros' retry, timeout and cancellation policy.
/// </summary>
/// <remarks>
/// This sits above <see cref="IChatProvider" /> deliberately. Retry logic that lives inside each
/// provider drifts - one grows jitter, another forgets that cancellation is not transient - and the
/// bugs are then N times over. Provider SDKs that ship their own retries have them turned off at
/// construction for the same reason: one authority, not two stacked multiplicatively.
/// </remarks>
internal sealed class ChatExecutor(ILogger? logger = null)
{
    private readonly ILogger Logger = logger ?? NullLogger.Instance;

    public async Task<OuroResponseBase> ExecuteAsync(IChatProvider provider, List<OuroMessage> messages,
        ChatOptions options, CancellationToken cancellationToken = default)
    {
        var delay = BackoffPolicy.GetBackoffPolicy(options.UseExponentialBackOff);
        var attemptTimeout = options.Timeout ?? Constants.DefaultAttemptTimeout;

        Logger.LogInformation(
            "Sending {count} chat messages to {provider} with UseExponentialBackoff = {useBackoff}",
            messages.Count, provider.Kind, options.UseExponentialBackOff);

        var policyResult = await Policy
            .Handle<Exception>(IsTransient)
            .OrResult<ProviderAttempt>(attempt => attempt.IsRetryable)
            .WaitAndRetryAsync(
                delay,
                (outcome, timespan, retryAttempt, context) =>
                {
                    Logger.LogWarning("Delaying for {delay}ms, then attempting retry {retry}.",
                        timespan.TotalMilliseconds, retryAttempt);
                })
            .ExecuteAndCaptureAsync(async policyToken =>
                {
                    // Each attempt gets its own budget, so a retry is not penalised by time the
                    // previous one burned. HttpClient.Timeout cannot express this - it is
                    // per-client - which is why the transports run with no timeout of their own.
                    using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(policyToken);
                    attemptCts.CancelAfter(attemptTimeout);

                    return await provider.SendAsync(messages, options, attemptCts.Token);
                },
                cancellationToken);

        // ExecuteAndCaptureAsync captures unhandled exceptions as well as handled ones, so nothing
        // propagates out of the policy - cancellation included. The caller's token is the one case
        // that must still throw, so check it explicitly rather than waiting for an exception.
        cancellationToken.ThrowIfCancellationRequested();

        return Unwrap(policyResult, provider, attemptTimeout);
    }

    private static OuroResponseBase Unwrap(PolicyResult<ProviderAttempt> policyResult, IChatProvider provider,
        TimeSpan attemptTimeout)
    {
        if (policyResult.Outcome == OutcomeType.Successful)
            return policyResult.Result?.Response
                   ?? new OuroResponseInternalError(
                       $"The {provider.Kind} provider reported success but returned nothing. This should never happen.");

        // A blown attempt budget arrives as a captured exception rather than a throw. Discriminate
        // on the exception itself; FaultType only says whether the policy chose to retry it.
        if (policyResult.FinalException is OperationCanceledException)
            return new OuroResponseInternalError(
                $"The request exceeded its {attemptTimeout.TotalSeconds:0}s attempt timeout. " +
                "Raise ChatOptions.Timeout if the work legitimately takes this long.");

        if (policyResult.FinalException is { } exception)
            return new OuroResponseInternalError("Exception calling endpoint: " + exception.Message);

        // Retries exhausted on a retryable failure - hand back the provider's own last word on it,
        // which carries the real error code rather than a summary of it.
        if (policyResult.FinalHandledResult?.Response is { } exhausted)
            return exhausted;

        return new OuroResponseInternalError(
            $"The retry policy reported an unexpected fault type: {policyResult.FaultType}.");
    }

    /// <summary>
    /// Whether an exception is worth another attempt.
    /// </summary>
    /// <remarks>
    /// This was previously Handle&lt;Exception&gt;() - retry anything at all. That turns actively
    /// harmful once calls can run for minutes: a turn that blows its timeout throws
    /// OperationCanceledException, which the old predicate happily retried five more times. You paid
    /// for six executions of the same work and still ended up with a failure.
    /// </remarks>
    private static bool IsTransient(Exception ex)
    {
        // Walk the chain rather than checking one level. SDKs wrap transport failures, sometimes
        // more than once, and an AggregateException can hide several - a socket error two levels
        // down is exactly as transient as one at the top.
        var chain = Unwrap(ex).ToList();

        // Cancellation anywhere in the chain wins, and is checked across the whole chain before
        // looking for transience. Order matters: a cancelled request frequently surfaces as an
        // HttpRequestException wrapping a TaskCanceledException, so a single pass would match the
        // wrapper, call it transient, and retry the exact thing this guard exists to prevent.
        if (chain.Any(inner => inner is OperationCanceledException))
            return false;

        return chain.Any(inner =>
            inner is HttpRequestException or IOException or TimeoutException or SocketException);
    }

    private static IEnumerable<Exception> Unwrap(Exception ex)
    {
        if (ex is AggregateException aggregate)
        {
            foreach (var inner in aggregate.Flatten().InnerExceptions)
            foreach (var unwrapped in Unwrap(inner))
                yield return unwrapped;

            yield break;
        }

        for (var current = ex; current is not null; current = current.InnerException)
            yield return current;
    }
}
