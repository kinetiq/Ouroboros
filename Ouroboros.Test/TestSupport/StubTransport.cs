using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenAI.Responses;

namespace Ouroboros.Test.TestSupport;

/// <summary>
/// A fake HTTP transport, so the request handler can be exercised end to end - mapper, retry
/// policy and response mapping included - without a network.
/// </summary>
/// <remarks>
/// Counts attempts, which is the only way to prove the retry policy did or did not retry.
/// </remarks>
internal sealed class StubTransport(string responseJson, TimeSpan? delay = null) : HttpMessageHandler
{
    private int CallCount;

    /// <summary>
    /// How many times the transport was invoked. One means no retry happened.
    /// </summary>
    public int Calls => Volatile.Read(ref CallCount);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref CallCount);

        if (delay is { } stall)
            await Task.Delay(stall, cancellationToken);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };
    }

    /// <summary>
    /// Wraps this transport in a ResponsesClient configured exactly as OuroClient configures its
    /// own: no client timeout, no network timeout, no SDK-level retries.
    /// </summary>
    /// <remarks>
    /// The settings are not incidental. Leaving NetworkTimeout at its 100s default would cap
    /// ChatOptions.Timeout, and leaving retries on would multiply every attempt count these tests
    /// assert on - so a stub built differently from the real client would prove nothing about it.
    /// </remarks>
    public ResponsesClient ToClient()
    {
        return ToClient(this);
    }

    internal static ResponsesClient ToClient(HttpMessageHandler handler)
    {
        return new ResponsesClient(
            new ApiKeyCredential("test-key"),
            new ResponsesClientOptions
            {
                Transport = new HttpClientPipelineTransport(
                    new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan }),
                NetworkTimeout = Timeout.InfiniteTimeSpan,
                RetryPolicy = new ClientRetryPolicy(maxRetries: 0)
            });
    }

    /// <summary>
    /// A minimal well-formed Responses payload carrying one assistant message.
    /// </summary>
    /// <param name="content">The assistant's text.</param>
    /// <param name="status">Response status - "completed" or "incomplete".</param>
    /// <param name="incompleteReason">
    /// Populates incomplete_details when the status is "incomplete". This is where truncation is
    /// reported on this API; there is no finish_reason field.
    /// </param>
    public static string Response(string content, string status = "completed",
        string? incompleteReason = null)
    {
        // Serialized rather than interpolated so content containing quotes or newlines stays valid.
        var encoded = JsonSerializer.Serialize(content);

        var incomplete = incompleteReason is null
            ? "null"
            : $$"""{ "reason": "{{incompleteReason}}" }""";

        return $$"""
            {
              "id": "resp-test",
              "object": "response",
              "created_at": 1,
              "status": "{{status}}",
              "model": "gpt-5.4-mini",
              "incomplete_details": {{incomplete}},
              "output": [
                {
                  "type": "message",
                  "id": "msg-test",
                  "status": "completed",
                  "role": "assistant",
                  "content": [ { "type": "output_text", "text": {{encoded}}, "annotations": [] } ]
                }
              ],
              "parallel_tool_calls": true,
              "tool_choice": "auto",
              "tools": [],
              "usage": {
                "input_tokens": 3,
                "output_tokens": 1,
                "total_tokens": 4
              }
            }
            """;
    }
}
