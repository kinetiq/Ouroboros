using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Betalgo.Ranul.OpenAI;
using Betalgo.Ranul.OpenAI.Managers;

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
    /// Wraps this transport in an OpenAIService with no client-level timeout, matching how
    /// OuroClient builds its own - the deadline is per attempt, not per client.
    /// </summary>
    public OpenAIService ToApi()
    {
        return new OpenAIService(
            new OpenAIOptions { ApiKey = "test-key" },
            new HttpClient(this) { Timeout = Timeout.InfiniteTimeSpan });
    }

    /// <summary>
    /// A minimal well-formed chat completion payload.
    /// </summary>
    public static string ChatCompletion(string content, string finishReason = "stop")
    {
        // Serialized rather than interpolated so content containing quotes or newlines stays valid.
        var encoded = JsonSerializer.Serialize(content);

        return $$"""
            {
              "id": "chatcmpl-test",
              "object": "chat.completion",
              "created": 1,
              "model": "gpt-5.4-mini",
              "choices": [
                {
                  "index": 0,
                  "message": { "role": "assistant", "content": {{encoded}} },
                  "finish_reason": "{{finishReason}}"
                }
              ],
              "usage": { "prompt_tokens": 3, "completion_tokens": 1, "total_tokens": 4 }
            }
            """;
    }
}
