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
using Ouroboros.LargeLanguageModels.Providers;
using Ouroboros.Responses;
using Polly;

namespace Ouroboros.LargeLanguageModels.Resilience;

/// <summary>
/// Runs one provider operation under Ouroboros' retry, timeout and cancellation policy.
/// </summary>
/// <remarks>
/// Pulled out of ChatExecutor so that every endpoint shares one copy of the policy. The attempt is a
/// delegate rather than an IChatProvider because not every endpoint is a chat: anything that can
/// report its outcome as a ProviderAttempt can run here, and gets the same backoff, the same
/// per-attempt budget and the same judgement of what counts as transient.
///
/// Callers above this decide what to do with the outcome. ChatExecutor reads FailoverEligible to
/// walk its chain; an endpoint with no chain can ignore it.
/// </remarks>
internal sealed class AttemptRunner(ILogger? logger = null)
{
    private readonly ILogger Logger = logger ?? NullLogger.Instance;

    /// <param name="attempt">
    /// Makes exactly one attempt. Receives a token that combines the caller's cancellation with the
    /// per-attempt budget.
    /// </param>
    /// <param name="provider">Which provider is being called, for error messages.</param>
    /// <param name="useExponentialBackOff">False makes exactly one attempt.</param>
    /// <param name="attemptTimeout">The budget each attempt gets on its own.</param>
    /// <param name="timeoutSetting">
    /// The option a caller raises to lengthen the budget, named in the timeout error so the message
    /// points at the right knob.
    /// </param>
    public async Task<ExecutionOutcome> RunAsync(Func<CancellationToken, Task<ProviderAttempt>> attempt,
        OuroProvider provider, bool useExponentialBackOff, TimeSpan attemptTimeout, string timeoutSetting,
        CancellationToken cancellationToken)
    {
        var delay = BackoffPolicy.GetBackoffPolicy(useExponentialBackOff);

        var policyResult = await Policy
            .Handle<Exception>(IsTransient)
            .OrResult<ProviderAttempt>(result => result.IsRetryable)
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

                    return await attempt(attemptCts.Token);
                },
                cancellationToken);

        // ExecuteAndCaptureAsync captures unhandled exceptions too, so nothing escapes the policy,
        // cancellation included. Reported here instead of thrown, so each caller can decide:
        // ExecuteAsync throws, the chain first returns what completed.
        //
        // Checked against the token, not the exception. A caller-cancelled attempt and a blown
        // attempt budget throw the same type, and the second message would name a timeout nobody
        // set.
        if (cancellationToken.IsCancellationRequested)
        {
            return new ExecutionOutcome(
                new OuroResponseInternalError("The call was cancelled by its caller."),
                FailoverEligible: false);
        }

        return Unwrap(policyResult, provider, attemptTimeout, timeoutSetting);
    }

    /// <summary>
    /// Turns the policy's outcome into a response, and says whether another provider deserves a go.
    /// </summary>
    /// <remarks>
    /// Eligibility is narrower than "it failed". A failure the policy never handled is a
    /// deterministic one: a mapper that threw, a response type that will not turn into a schema.
    /// That fails the same way on the next provider, for twice the money.
    ///
    /// So eligibility follows what the policy did. Results and exceptions it handled and then
    /// exhausted qualify, as does the attempt timeout, which it deliberately never handles.
    /// </remarks>
    private static ExecutionOutcome Unwrap(PolicyResult<ProviderAttempt> policyResult, OuroProvider provider,
        TimeSpan attemptTimeout, string timeoutSetting)
    {
        if (policyResult.Outcome == OutcomeType.Successful)
        {
            var response = policyResult.Result?.Response
                           ?? new OuroResponseInternalError(
                               $"The {provider} provider reported success but returned nothing. This should never happen.");

            return new ExecutionOutcome(response, FailoverEligible: false);
        }

        // A blown attempt budget arrives as a captured exception rather than a throw. Discriminate
        // on the exception itself; FaultType only says whether the policy chose to retry it.
        if (policyResult.FinalException is OperationCanceledException)
        {
            return new ExecutionOutcome(
                new OuroResponseInternalError(
                    $"The request exceeded its {attemptTimeout.TotalSeconds:0}s attempt timeout. " +
                    $"Raise {timeoutSetting} if the work legitimately takes this long."),
                FailoverEligible: true);
        }

        if (policyResult.FinalException is { } exception)
        {
            // Handled means IsTransient said yes and the retries then ran out. Anything else got
            // here on its first throw and is the request's own fault, not the provider's.
            var transient = policyResult.FaultType == FaultType.ExceptionHandledByThisPolicy;

            return new ExecutionOutcome(
                new OuroResponseInternalError("Exception calling endpoint: " + exception.Message),
                FailoverEligible: transient);
        }

        // Retries exhausted on a retryable failure - hand back the provider's own last word on it,
        // which carries the real error code rather than a summary of it.
        if (policyResult.FinalHandledResult?.Response is { } exhausted)
            return new ExecutionOutcome(exhausted, FailoverEligible: true);

        return new ExecutionOutcome(
            new OuroResponseInternalError(
                $"The retry policy reported an unexpected fault type: {policyResult.FaultType}."),
            FailoverEligible: false);
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
