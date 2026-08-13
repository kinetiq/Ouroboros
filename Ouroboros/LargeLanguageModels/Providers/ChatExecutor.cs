using System;
using System.Collections.Generic;
using System.Diagnostics;
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
/// Sits above <see cref="IChatProvider" /> so there is one retry policy, not one per provider.
/// Per-provider policies drift: one grows jitter, another forgets that cancellation is not
/// transient. Provider SDKs ship their own retries, which OuroClient turns off at construction for
/// the same reason - two policies stack multiplicatively.
/// </remarks>
internal sealed class ChatExecutor(ILogger? logger = null)
{
    private readonly ILogger Logger = logger ?? NullLogger.Instance;

    public async Task<OuroResponseBase> ExecuteAsync(IChatProvider provider, List<OuroMessage> messages,
        ChatOptions options, CancellationToken cancellationToken = default)
    {
        var outcome = await RunAsync(provider, messages, options, cancellationToken);

        // The single-call path throws on caller cancellation, per the .NET contract and exactly as
        // it always has. RunAsync reports it instead of throwing so the chain below can log the
        // attempts that already ran; here there are none to lose.
        cancellationToken.ThrowIfCancellationRequested();

        return outcome.Response;
    }

    /// <summary>
    /// Runs each entry of a fallback chain in turn, stopping at the first that settles the call.
    /// </summary>
    /// <remarks>
    /// Only a transient exhaustion moves to the next entry; <see cref="Unwrap" /> decides which
    /// those are. Each entry runs a full attempt of its own, so it gets a fresh retry budget and
    /// fresh per-attempt timeouts. Nothing waits between entries, because the entry that failed
    /// already served out its backoff.
    ///
    /// Cancellation is reported through <see cref="ChainResult.Cancelled" /> instead of throwing.
    /// Attempts that already ran cost money, and throwing from inside the loop would discard their
    /// record before anything could log it.
    /// </remarks>
    public async Task<ChainResult> ExecuteChainAsync(IReadOnlyList<ChainEntry> chain,
        List<OuroMessage> messages, CancellationToken cancellationToken = default)
    {
        if (chain.Count == 0)
        {
            return new ChainResult([],
                new OuroResponseInternalError("A fallback chain was run with no entries in it."),
                Cancelled: false);
        }

        var attempts = new List<AttemptRecord>();

        for (var index = 0; index < chain.Count; index++)
        {
            var entry = chain[index];
            var stopwatch = Stopwatch.StartNew();

            var outcome = await RunAsync(entry.Provider, messages, entry.Options, cancellationToken);

            stopwatch.Stop();

            // A cancelled attempt is not recorded: it did not finish, so there is no outcome worth
            // logging. Earlier entries that did finish are still returned - they cost real money and
            // their record is the thing throwing from inside the loop would have thrown away.
            if (cancellationToken.IsCancellationRequested)
                return new ChainResult(attempts, outcome.Response, Cancelled: true);

            var durationMs = (int)stopwatch.ElapsedMilliseconds;

            // Each attempt's response carries its own duration. The total across the chain is
            // stamped onto whichever response is handed back, by ChatAsync.
            outcome.Response.DurationMs = durationMs;
            attempts.Add(new AttemptRecord(entry.Model, outcome.Response, durationMs));

            var isLast = index == chain.Count - 1;

            if (!outcome.FailoverEligible || isLast)
                return new ChainResult(attempts, outcome.Response, Cancelled: false);

            Logger.LogWarning(
                "{Model} did not settle the call, so failing over to {NextModel}. Last word: {Reason}",
                entry.Model, chain[index + 1].Model, outcome.Response.ResponseText);
        }

        // Not reachable: the loop returns on its last iteration.
        return new ChainResult(attempts, attempts[^1].Response, Cancelled: false);
    }

    private async Task<ExecutionOutcome> RunAsync(IChatProvider provider, List<OuroMessage> messages,
        ChatOptions options, CancellationToken cancellationToken)
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

        return Unwrap(policyResult, provider, attemptTimeout);
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
    private static ExecutionOutcome Unwrap(PolicyResult<ProviderAttempt> policyResult, IChatProvider provider,
        TimeSpan attemptTimeout)
    {
        if (policyResult.Outcome == OutcomeType.Successful)
        {
            var response = policyResult.Result?.Response
                           ?? new OuroResponseInternalError(
                               $"The {provider.Kind} provider reported success but returned nothing. This should never happen.");

            return new ExecutionOutcome(response, FailoverEligible: false);
        }

        // A blown attempt budget arrives as a captured exception rather than a throw. Discriminate
        // on the exception itself; FaultType only says whether the policy chose to retry it.
        if (policyResult.FinalException is OperationCanceledException)
        {
            return new ExecutionOutcome(
                new OuroResponseInternalError(
                    $"The request exceeded its {attemptTimeout.TotalSeconds:0}s attempt timeout. " +
                    "Raise ChatOptions.Timeout if the work legitimately takes this long."),
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
