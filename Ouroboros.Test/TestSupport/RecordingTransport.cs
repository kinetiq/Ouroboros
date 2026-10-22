using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ouroboros.Test.TestSupport;

/// <summary>
/// Replays canned responses with any status code, and keeps each request it was sent.
/// </summary>
/// <remarks>
/// The other transports only answer 200, which is all an SDK-backed provider needs: the SDK turns
/// error statuses into exceptions before the provider sees them. The Decisions provider reads the
/// status itself, so its tests need a transport that can send a 429 or a 400.
/// </remarks>
internal sealed class RecordingTransport(params (HttpStatusCode Status, string Body)[] responses) : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode Status, string Body)> Remaining = new(responses);

    public List<RecordedRequest> Requests { get; } = [];

    public int Calls => Requests.Count;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);

        Requests.Add(new RecordedRequest(request.Method, request.RequestUri, request.Headers.Authorization?.ToString(),
            body));

        if (Remaining.Count == 0)
            throw new InvalidOperationException($"The transport ran out of responses after {Calls} calls.");

        var (status, responseBody) = Remaining.Dequeue();

        return new HttpResponseMessage(status)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        };
    }

    /// <summary>
    /// An HttpClient over this transport, with no timeout, as OuroClient builds its own.
    /// </summary>
    public HttpClient ToHttpClient()
    {
        return new HttpClient(this) { Timeout = Timeout.InfiniteTimeSpan };
    }
}

internal sealed record RecordedRequest(HttpMethod Method, Uri? Uri, string? Authorization, string Body);
