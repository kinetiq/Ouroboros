using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Betalgo.Ranul.OpenAI;
using Betalgo.Ranul.OpenAI.Managers;

namespace Ouroboros.Test.TestSupport;

/// <summary>
/// A transport that always throws, for exercising the retry predicate.
/// </summary>
/// <remarks>
/// Counting attempts is the whole point: whether an exception is retried is invisible from the
/// response alone, since an exhausted retry and a first-attempt refusal both come back as a
/// failure. Only the call count tells them apart.
/// </remarks>
internal sealed class ThrowingTransport(Func<Exception> exception) : HttpMessageHandler
{
    private int CallCount;

    public int Calls => Volatile.Read(ref CallCount);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref CallCount);

        throw exception();
    }

    public OpenAIService ToApi()
    {
        return new OpenAIService(
            new OpenAIOptions { ApiKey = "test-key" },
            new HttpClient(this) { Timeout = Timeout.InfiniteTimeSpan });
    }
}
