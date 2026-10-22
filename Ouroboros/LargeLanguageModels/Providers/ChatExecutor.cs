using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.LargeLanguageModels.Resilience;
using Ouroboros.Responses;

namespace Ouroboros.LargeLanguageModels.Providers;

/// <summary>
/// Runs a provider attempt under Ouroboros' retry, timeout and cancellation policy.
/// </summary>
/// <remarks>
/// Sits above <see cref="IChatProvider" /> so there is one retry policy, not one per provider.
/// Per-provider policies drift: one grows jitter, another forgets that cancellation is not
/// transient. Provider SDKs ship their own retries, which OuroClient turns off at construction for
/// the same reason - two policies stack multiplicatively.
///
/// The policy itself lives in <see cref="AttemptRunner" />, shared with endpoints that are not
/// chats. What stays here is the chat-shaped part: the fallback chain and its bookkeeping.
/// </remarks>
internal sealed class ChatExecutor(ILogger? logger = null)
{
    private readonly ILogger Logger = logger ?? NullLogger.Instance;

    private readonly AttemptRunner Runner = new(logger);

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
    /// Only a transient exhaustion moves to the next entry; <see cref="AttemptRunner" /> decides which
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

    private Task<ExecutionOutcome> RunAsync(IChatProvider provider, List<OuroMessage> messages,
        ChatOptions options, CancellationToken cancellationToken)
    {
        Logger.LogInformation(
            "Sending {count} chat messages to {provider} with UseExponentialBackoff = {useBackoff}",
            messages.Count, provider.Kind, options.UseExponentialBackOff);

        return Runner.RunAsync(
            attemptToken => provider.SendAsync(messages, options, attemptToken),
            provider.Kind,
            options.UseExponentialBackOff,
            options.Timeout ?? Constants.DefaultAttemptTimeout,
            "ChatOptions.Timeout",
            cancellationToken);
    }
}
