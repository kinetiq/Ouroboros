using System;
using System.Threading;
using System.Threading.Tasks;
using Betalgo.Ranul.OpenAI.Managers;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.LargeLanguageModels.Providers;
using Ouroboros.Responses;
using Ouroboros.Test.TestSupport;

namespace Ouroboros.Test.Resilience;

/// <summary>
/// Pins what the retry policy will and will not retry.
/// </summary>
/// <remarks>
/// The executor used to retry on Handle&lt;Exception&gt;() - anything at all. That was survivable
/// while every call took a few seconds, and stops being survivable once a call can run for
/// minutes: a turn that blew its own timeout threw OperationCanceledException, which the policy
/// dutifully retried five more times. Six executions of the same work, billed, then a failure
/// anyway. These tests count attempts at the transport so that cannot come back.
///
/// They exercise the policy through the OpenAI provider, but the policy itself is provider-neutral
/// - it lives in ChatExecutor precisely so there is only one copy of this behaviour to get right.
/// </remarks>
public class RetryDisciplineTests
{
    [Fact]
    public async Task An_Attempt_That_Blows_Its_Timeout_Is_Not_Retried()
    {
        var transport = Stalling();

        var response = await Execute(transport, new ChatOptions { Timeout = TimeSpan.FromMilliseconds(50) });

        Assert.Equal(1, transport.Calls);
        Assert.IsType<OuroResponseInternalError>(response);
    }

    /// <summary>
    /// A blown budget is our decision rather than the caller's, so it comes back as a response.
    /// The message has to name the knob, or the only fix a caller can find is "retry it".
    /// </summary>
    [Fact]
    public async Task A_Blown_Timeout_Returns_A_Failure_Naming_The_Setting()
    {
        var response = await Execute(Stalling(), new ChatOptions { Timeout = TimeSpan.FromMilliseconds(50) });

        Assert.False(response.Success);
        Assert.Contains("ChatOptions.Timeout", response.ResponseText);
    }

    /// <summary>
    /// Caller cancellation throws rather than returning a failure - the .NET contract, and what
    /// keeps a deliberate stop distinguishable from the provider falling over.
    /// </summary>
    [Fact]
    public async Task Caller_Cancellation_Throws_And_Does_Not_Retry()
    {
        var transport = Stalling();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Execute(transport, new ChatOptions(), cts.Token));

        Assert.Equal(1, transport.Calls);
    }

    /// <summary>
    /// The whole point of the per-attempt budget: a call slower than a conventional HTTP default
    /// still succeeds. Provider-side code execution routinely will be.
    /// </summary>
    [Fact]
    public async Task A_Slow_Call_Inside_Its_Budget_Still_Succeeds()
    {
        var transport = new StubTransport(StubTransport.ChatCompletion("ok"), TimeSpan.FromMilliseconds(150));

        var response = await Execute(transport, new ChatOptions { Timeout = TimeSpan.FromSeconds(10) });

        Assert.Equal(1, transport.Calls);
        Assert.True(response.Success);
        Assert.Equal("ok", response.ResponseText);
    }

    /// <summary>
    /// A timeout wrapped by the SDK still must not be retried.
    /// </summary>
    /// <remarks>
    /// The predicate walks the whole exception chain rather than checking one level. SDKs wrap
    /// transport failures, sometimes twice, so a cancellation buried two deep has to be recognised
    /// as cancellation - otherwise the very case this guard exists for slips straight past it.
    /// </remarks>
    [Fact]
    public async Task A_Wrapped_Cancellation_Is_Still_Not_Retried()
    {
        var transport = new ThrowingTransport(() => new InvalidOperationException(
            "SDK wrapper", new HttpRequestException(
                "transport", new TaskCanceledException("the real cause"))));

        var response = await Execute(transport.ToApi(), new ChatOptions());

        // One attempt: the HttpRequestException in the middle looks transient in isolation, so a
        // chain walk that stopped at the first match would have retried this five more times.
        Assert.Equal(1, transport.Calls);
        Assert.False(response.Success);
    }

    private static StubTransport Stalling()
    {
        return new StubTransport(StubTransport.ChatCompletion("ok"), TimeSpan.FromSeconds(30));
    }

    private static Task<OuroResponseBase> Execute(StubTransport transport, ChatOptions options,
        CancellationToken cancellationToken = default)
    {
        return Execute(transport.ToApi(), options, cancellationToken);
    }

    private static Task<OuroResponseBase> Execute(OpenAIService api, ChatOptions options,
        CancellationToken cancellationToken = default)
    {
        // ChatExecutor sits below the layer that resolves the model, so these tests supply
        // one. The mappers now demand a resolved model rather than defaulting to a GPT id -
        // which is exactly the bypass that guard exists to catch.
        options.Model ??= OuroModels.Gpt_5_4_mini;

        return new ChatExecutor().ExecuteAsync(
            new OpenAiChatProvider(api),
            [OuroMessage.FromUser("hi")],
            options,
            cancellationToken);
    }
}
