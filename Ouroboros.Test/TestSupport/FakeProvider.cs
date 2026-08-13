using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.LargeLanguageModels.Providers;
using Ouroboros.Responses;

namespace Ouroboros.Test.TestSupport;

/// <summary>
/// A provider that returns whatever the test tells it to, and counts how often it was asked.
/// </summary>
/// <remarks>
/// Failover is about what the executor does with an outcome, not about how any particular vendor
/// produces one. Driving it with real transports would mean waiting out the real backoff schedule -
/// about 155 seconds per exhausted entry - to assert on control flow. This lets each case state its
/// outcome directly.
/// </remarks>
internal sealed class FakeProvider(OuroProvider kind, Func<int, ProviderAttempt> respond) : IChatProvider
{
    private int CallCount;

    public OuroProvider Kind => kind;

    /// <summary>
    /// How many times this provider was asked. Zero proves the chain never reached it.
    /// </summary>
    public int Calls => Volatile.Read(ref CallCount);

    /// <summary>
    /// The options this provider was last handed, so a test can see what the chain did to them.
    /// </summary>
    public ChatOptions? LastOptions { get; private set; }

    public Task<ProviderAttempt> SendAsync(List<OuroMessage> messages, ChatOptions options,
        CancellationToken cancellationToken)
    {
        var call = Interlocked.Increment(ref CallCount);

        LastOptions = options;

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(respond(call));
    }

    /// <summary>
    /// Always succeeds.
    /// </summary>
    public static FakeProvider Succeeds(OuroProvider kind, string text = "ok")
    {
        return new FakeProvider(kind, _ => ProviderAttempt.Final(new OuroResponseSuccess(text) { Model = text }));
    }

    /// <summary>
    /// Always returns a retryable failure, so the policy exhausts its budget on it.
    /// </summary>
    public static FakeProvider AlwaysRetryable(OuroProvider kind, string message = "rate limited")
    {
        return new FakeProvider(kind,
            _ => ProviderAttempt.Retryable(new OuroResponseProviderError(kind.ToString(), "429", message)));
    }

    /// <summary>
    /// Always returns a failure the provider considers settled - a bad request, an auth problem.
    /// </summary>
    public static FakeProvider AlwaysFinal(OuroProvider kind, string message = "bad request")
    {
        return new FakeProvider(kind,
            _ => ProviderAttempt.Final(new OuroResponseProviderError(kind.ToString(), "400", message)));
    }

    /// <summary>
    /// Always throws the given exception.
    /// </summary>
    public static FakeProvider Throws(OuroProvider kind, Func<Exception> exception)
    {
        return new FakeProvider(kind, _ => throw exception());
    }
}
