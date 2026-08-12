using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AnthropicSdk = Anthropic;

namespace Ouroboros.Test.TestSupport;

/// <summary>
/// Replays a queue of canned responses, and keeps every request body it was sent.
/// </summary>
/// <remarks>
/// A paused turn is only observable across more than one request: the first pauses, the next
/// carries what the first produced. A transport with one fixed answer cannot express that, and the
/// interesting assertion is about the <em>second</em> request rather than any response.
/// </remarks>
internal sealed class SequencedTransport(params string[] responses) : HttpMessageHandler
{
    private readonly Queue<string> Remaining = new(responses);

    /// <summary>
    /// The body of each request, in order. Index 1 is the continuation.
    /// </summary>
    public List<string> Requests { get; } = [];

    public int Calls => Requests.Count;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request.Content is null
            ? ""
            : await request.Content.ReadAsStringAsync(cancellationToken));

        if (Remaining.Count == 0)
            throw new InvalidOperationException($"The transport ran out of responses after {Calls} calls.");

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(Remaining.Dequeue(), Encoding.UTF8, "application/json")
        };
    }

    /// <summary>
    /// An Anthropic client wired to this transport, configured as OuroClient configures its own.
    /// </summary>
    public AnthropicSdk.AnthropicClient ToAnthropicClient()
    {
        return new AnthropicSdk.AnthropicClient
        {
            ApiKey = "test-key",
            HttpClient = new HttpClient(this) { Timeout = Timeout.InfiniteTimeSpan },

            // ChatExecutor is the single retry authority - the same rule the real client applies.
            MaxRetries = 0
        };
    }

    /// <summary>
    /// A Messages response carrying the given raw content blocks.
    /// </summary>
    public static string Message(string stopReason, string contentBlocksJson, int inputTokens = 10,
        int outputTokens = 5)
    {
        return $$"""
            {
              "id": "msg_test",
              "type": "message",
              "role": "assistant",
              "model": "claude-opus-5",
              "content": {{contentBlocksJson}},
              "stop_reason": {{JsonSerializer.Serialize(stopReason)}},
              "stop_sequence": null,
              "usage": { "input_tokens": {{inputTokens}}, "output_tokens": {{outputTokens}} }
            }
            """;
    }

    /// <summary>
    /// A server_tool_use block - the model asking to run something, with no result yet.
    /// </summary>
    public static string ToolUseBlock(string toolUseId, string command) =>
        $$"""
        [{
          "type":"server_tool_use",
          "id":{{JsonSerializer.Serialize(toolUseId)}},
          "name":"bash_code_execution",
          "input":{"command":{{JsonSerializer.Serialize(command)}}}
        }]
        """;

    /// <summary>
    /// The result for a tool use, which a pause can separate from the invocation it belongs to.
    /// </summary>
    public static string ToolResultBlock(string toolUseId, string stdout, int returnCode = 0) =>
        $$"""
        [{
          "type":"bash_code_execution_tool_result",
          "tool_use_id":{{JsonSerializer.Serialize(toolUseId)}},
          "content":{
            "type":"bash_code_execution_result",
            "stdout":{{JsonSerializer.Serialize(stdout)}},
            "stderr":"",
            "return_code":{{returnCode}},
            "content":[]
          }
        }]
        """;

    public static string TextBlock(string text) =>
        $$"""[{"type":"text","text":{{JsonSerializer.Serialize(text)}},"citations":null}]""";

    /// <summary>
    /// A thinking block with a signature, plus some text. The signature is the point: the API
    /// verifies it, so a continuation that mangles it is rejected outright.
    /// </summary>
    public static string ThinkingAndText(string signature, string text) =>
        $$"""
        [
          {"type":"thinking","thinking":"working on it","signature":{{JsonSerializer.Serialize(signature)}}},
          {"type":"text","text":{{JsonSerializer.Serialize(text)}},"citations":null}
        ]
        """;
}
