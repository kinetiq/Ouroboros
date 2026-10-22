// The Decisions surface is experimental until OpenAI's API is GA. See DecisionRequestTypeTests.
#pragma warning disable OURO001

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Config;
using Ouroboros.Decisions;
using Ouroboros.Responses;
using Ouroboros.Test.TestSupport;

namespace Ouroboros.Test.Decisions;

/// <summary>
/// DecideAsync runs through the real client, provider and retry policy, with only the network faked.
/// </summary>
/// <remarks>
/// The provider tests prove each attempt is labelled correctly. These prove the label is acted on:
/// a retryable failure is retried, a timeout names the right setting, and cancellation throws.
/// </remarks>
public class DecideAsyncTests
{
    private const string Answer = """
        { "model": "gpt-6-luna", "answers": [ { "type": "predicate", "name": "angry", "probability": 0.8 } ] }
        """;

    private const string RateLimited = """{ "error": { "message": "Slow down.", "type": "rate_limit", "code": null } }""";

    private static readonly OuroDecisionRequest Request = new()
    {
        Text = "This is the third time I have asked!",
        Questions = [new OuroPredicate("angry", "Is the customer angry?")]
    };

    private static OuroClient Client(HttpMessageHandler handler, OuroborosOptions? options = null)
    {
        return new OuroClient(options ?? new OuroborosOptions { OpenAiApiKey = "test-key" }, null, null, handler);
    }

    [Fact]
    public async Task Returns_The_Answers_And_The_Call_Duration()
    {
        using var client = Client(new RecordingTransport((HttpStatusCode.OK, Answer)));

        var response = await client.DecideAsync(Request, cancellationToken: TestContext.Current.CancellationToken);

        var decision = Assert.IsType<OuroDecisionResponse>(response);
        Assert.Equal(0.8, decision.Get<OuroPredicateAnswer>("angry")?.Probability);
        Assert.True(decision.DurationMs >= 0);
    }

    /// <summary>
    /// The one test that waits out a real backoff delay, about five seconds. It is the only proof the
    /// retryable flag reaches the policy through DecideAsync.
    /// </summary>
    [Fact]
    public async Task A_Rate_Limit_Is_Retried()
    {
        var transport = new RecordingTransport((HttpStatusCode.TooManyRequests, RateLimited), (HttpStatusCode.OK, Answer));
        using var client = Client(transport);

        var response = await client.DecideAsync(Request, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, transport.Calls);
        Assert.IsType<OuroDecisionResponse>(response);
    }

    [Fact]
    public async Task With_Backoff_Off_A_Rate_Limit_Is_Returned_After_One_Call()
    {
        var transport = new RecordingTransport((HttpStatusCode.TooManyRequests, RateLimited));
        using var client = Client(transport);

        var response = await client.DecideAsync(Request, new DecisionOptions { UseExponentialBackOff = false },
            TestContext.Current.CancellationToken);

        Assert.Equal(1, transport.Calls);
        Assert.IsType<OuroResponseProviderError>(response);
    }

    [Fact]
    public async Task A_Rejected_Request_Is_Not_Retried()
    {
        var transport = new RecordingTransport((HttpStatusCode.BadRequest,
            """{ "error": { "message": "Bad.", "type": "invalid_request_error", "code": "invalid" } }"""));
        using var client = Client(transport);

        var response = await client.DecideAsync(Request, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, transport.Calls);
        Assert.Equal("invalid", Assert.IsType<OuroResponseProviderError>(response).ErrorCode);
    }

    /// <summary>
    /// The timeout message names the setting to raise. Naming ChatOptions here would send the
    /// reader to the wrong class.
    /// </summary>
    [Fact]
    public async Task A_Blown_Timeout_Names_DecisionOptions()
    {
        using var client = Client(new StallingHandler());

        var response = await client.DecideAsync(Request,
            new DecisionOptions { Timeout = TimeSpan.FromMilliseconds(50), UseExponentialBackOff = false },
            TestContext.Current.CancellationToken);

        var error = Assert.IsType<OuroResponseInternalError>(response);
        Assert.Contains("DecisionOptions.Timeout", error.ResponseText);
    }

    [Fact]
    public async Task Cancellation_Throws()
    {
        using var client = Client(new StallingHandler());
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.DecideAsync(Request, cancellationToken: cts.Token));
    }

    /// <summary>
    /// Thrown, not returned, as ChatAsync does for a model whose provider has no key. It is a
    /// configuration mistake, not a provider failure.
    /// </summary>
    [Fact]
    public async Task A_Client_Without_An_OpenAI_Key_Throws_Naming_It()
    {
        using var client = Client(new RecordingTransport(), new OuroborosOptions { AnthropicApiKey = "test-key" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.DecideAsync(Request));

        Assert.Contains("OpenAi", ex.Message);
    }

    [Fact]
    public async Task A_Null_Request_Throws()
    {
        using var client = Client(new RecordingTransport());

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.DecideAsync(null!));
    }

    /// <summary>
    /// Resolves through the OuroClient registration rather than building a second client.
    /// </summary>
    /// <remarks>
    /// The factory is invoked by hand because the test project carries only the DI abstractions, not
    /// the container. The fake provider answers the one lookup the factory makes.
    /// </remarks>
    [Fact]
    public void AddOuroboros_Registers_IOuroDecisions_On_The_Same_Client()
    {
        var services = new ServiceCollection();
        services.AddOuroboros(options => options.OpenAiApiKey = "test-key");

        var registration = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IOuroDecisions));
        Assert.Equal(ServiceLifetime.Transient, registration.Lifetime);

        using var client = new OuroClient(new OuroborosOptions { OpenAiApiKey = "test-key" });
        var resolved = registration.ImplementationFactory!(new SingleServiceProvider(typeof(OuroClient), client));

        Assert.Same(client, resolved);
    }

    private sealed class SingleServiceProvider(Type type, object instance) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == type ? instance : null;
    }

    /// <summary>
    /// Holds every request until it is cancelled, so timeouts and cancellation can be observed.
    /// </summary>
    private sealed class StallingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        }
    }
}
