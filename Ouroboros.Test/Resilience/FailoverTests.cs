using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.LargeLanguageModels.Providers;
using Ouroboros.Responses;
using Ouroboros.Test.TestSupport;

namespace Ouroboros.Test.Resilience;

/// <summary>
/// What moves a call to the next provider, and what does not.
/// </summary>
/// <remarks>
/// The distinction these pin is the whole design: a provider being unwell is worth asking someone
/// else, and a request being wrong is not. Getting that backwards means either an outage takes the
/// feature down anyway, or every malformed request is paid for twice before failing.
///
/// Backoff is off throughout. The real schedule is about 155 seconds per exhausted entry, and none
/// of these assert on delays.
/// </remarks>
public class FailoverTests
{
    [Fact]
    public async Task An_Exhausted_Provider_Falls_Over_To_The_Next()
    {
        var primary = FakeProvider.AlwaysRetryable(OuroProvider.OpenAi);
        var fallback = FakeProvider.Succeeds(OuroProvider.Anthropic, "from the fallback");

        var result = await Run(primary, fallback);

        Assert.False(result.Cancelled);
        Assert.True(result.FinalResponse.Success);
        Assert.Equal("from the fallback", result.FinalResponse.ResponseText);

        // Both attempts are reported, in order, so a consumer logging these keeps a record of what
        // the first provider cost before it gave up.
        Assert.Equal(2, result.Attempts.Count);
        Assert.Equal(OuroModels.Gpt_5_4_mini, result.Attempts[0].Model);
        Assert.Equal(OuroModels.Claude_Opus_5, result.Attempts[1].Model);
        Assert.False(result.Attempts[0].Response.Success);
    }

    /// <summary>
    /// A settled failure stops the chain where it stands.
    /// </summary>
    /// <remarks>
    /// A 400 about the request would be a 400 on the next provider too. Failing over would spend a
    /// second call to arrive at the same answer.
    /// </remarks>
    [Fact]
    public async Task A_Final_Failure_Does_Not_Fall_Over()
    {
        var primary = FakeProvider.AlwaysFinal(OuroProvider.OpenAi);
        var fallback = FakeProvider.Succeeds(OuroProvider.Anthropic);

        var result = await Run(primary, fallback);

        Assert.Single(result.Attempts);
        Assert.Equal(0, fallback.Calls);
        Assert.False(result.FinalResponse.Success);
    }

    /// <summary>
    /// An exception the retry policy never handled is deterministic, so it does not fall over.
    /// </summary>
    /// <remarks>
    /// This is the case an "any failure fails over" rule gets wrong. A mapper that throws, or a
    /// response type that will not turn into a schema, throws identically on the next provider -
    /// the only difference being that the caller paid twice to find out.
    /// </remarks>
    [Fact]
    public async Task A_Deterministic_Exception_Does_Not_Fall_Over()
    {
        var primary = FakeProvider.Throws(OuroProvider.OpenAi,
            () => new InvalidOperationException("the mapper is broken"));

        var fallback = FakeProvider.Succeeds(OuroProvider.Anthropic);

        var result = await Run(primary, fallback);

        Assert.Single(result.Attempts);
        Assert.Equal(0, fallback.Calls);

        // One call, not six: a non-transient exception is not retried either.
        Assert.Equal(1, primary.Calls);
        Assert.Contains("the mapper is broken", result.FinalResponse.ResponseText);
    }

    /// <summary>
    /// A transient exception that outlasts its retries does fall over.
    /// </summary>
    [Fact]
    public async Task An_Exhausted_Transient_Exception_Falls_Over()
    {
        var primary = FakeProvider.Throws(OuroProvider.OpenAi, () => new IOException("connection reset"));
        var fallback = FakeProvider.Succeeds(OuroProvider.Anthropic, "recovered");

        var result = await Run(primary, fallback);

        Assert.Equal(2, result.Attempts.Count);
        Assert.Equal("recovered", result.FinalResponse.ResponseText);
    }

    /// <summary>
    /// A blown attempt budget falls over: the next provider may well be quicker.
    /// </summary>
    [Fact]
    public async Task An_Attempt_Timeout_Falls_Over()
    {
        var primary = new FakeProvider(OuroProvider.OpenAi, _ => throw new OperationCanceledException());
        var fallback = FakeProvider.Succeeds(OuroProvider.Anthropic, "in time");

        var result = await Run(primary, fallback);

        Assert.Equal(2, result.Attempts.Count);
        Assert.Equal("in time", result.FinalResponse.ResponseText);
    }

    /// <summary>
    /// When every entry is exhausted, the caller gets the last provider's own words.
    /// </summary>
    /// <remarks>
    /// Not a summary invented by the chain: the real error code is the only thing that tells anyone
    /// what actually went wrong.
    /// </remarks>
    [Fact]
    public async Task An_Exhausted_Chain_Returns_The_Last_Providers_Failure()
    {
        var primary = FakeProvider.AlwaysRetryable(OuroProvider.OpenAi, "openai is busy");
        var fallback = FakeProvider.AlwaysRetryable(OuroProvider.Anthropic, "anthropic is busy too");

        var result = await Run(primary, fallback);

        Assert.Equal(2, result.Attempts.Count);
        Assert.False(result.FinalResponse.Success);

        var failure = Assert.IsType<OuroResponseProviderError>(result.FinalResponse);

        Assert.Contains("anthropic is busy too", failure.ErrorDetails);
    }

    /// <summary>
    /// Cancellation surfaces the attempts that already finished.
    /// </summary>
    /// <remarks>
    /// The first attempt ran, took time, and cost money. Throwing from inside the loop would discard
    /// its record, so the caller would be billed for a chat their logs have no row for.
    /// </remarks>
    [Fact]
    public async Task Cancellation_Mid_Chain_Still_Reports_The_Completed_Attempt()
    {
        using var cts = new CancellationTokenSource();

        var primary = FakeProvider.AlwaysRetryable(OuroProvider.OpenAi);

        // Cancels as the fallback is entered, so the first attempt is complete and the second is not.
        var fallback = new FakeProvider(OuroProvider.Anthropic, _ =>
        {
            cts.Cancel();
            throw new OperationCanceledException();
        });

        var result = await Run(primary, fallback, cts.Token);

        Assert.True(result.Cancelled);

        // Only the attempt that finished. The cancelled one produced no outcome worth logging.
        Assert.Single(result.Attempts);
        Assert.Equal(OuroModels.Gpt_5_4_mini, result.Attempts[0].Model);
    }

    /// <summary>
    /// A single-entry chain behaves exactly as a plain call always has.
    /// </summary>
    [Fact]
    public async Task A_Single_Entry_Chain_Reports_One_Attempt()
    {
        var only = FakeProvider.Succeeds(OuroProvider.OpenAi, "just the one");

        var result = await Run(only);

        Assert.Single(result.Attempts);
        Assert.Equal("just the one", result.FinalResponse.ResponseText);
        Assert.False(result.Cancelled);
    }

    /// <summary>
    /// Each entry is handed its own model, so the mapper stamps the right id on the request.
    /// </summary>
    [Fact]
    public async Task Each_Entry_Runs_With_Its_Own_Model()
    {
        var primary = FakeProvider.AlwaysRetryable(OuroProvider.OpenAi);
        var fallback = FakeProvider.Succeeds(OuroProvider.Anthropic);

        await Run(primary, fallback);

        Assert.Equal(OuroModels.Gpt_5_4_mini, primary.LastOptions?.Model);
        Assert.Equal(OuroModels.Claude_Opus_5, fallback.LastOptions?.Model);
    }

    /// <summary>
    /// Runs a chain of [Gpt_5_4_mini, Claude_Opus_5], truncated to the providers given.
    /// </summary>
    private static Task<ChainResult> Run(FakeProvider primary, FakeProvider? fallback = null,
        CancellationToken cancellationToken = default)
    {
        // Backoff off: these assert on control flow, and the real schedule would add minutes per
        // exhausted entry without changing a single outcome.
        var options = new ChatOptions { UseExponentialBackOff = false };

        var chain = new List<ChainEntry>
        {
            Build(OuroModels.Gpt_5_4_mini, primary, options)
        };

        if (fallback is not null)
            chain.Add(Build(OuroModels.Claude_Opus_5, fallback, options));

        return new ChatExecutor().ExecuteChainAsync(chain, [OuroMessage.FromUser("hi")], cancellationToken);
    }

    private static ChainEntry Build(OuroModels model, IChatProvider provider, ChatOptions options)
    {
        var entry = options.Clone();
        entry.Model = model;

        return new ChainEntry(model, provider, entry);
    }
}
