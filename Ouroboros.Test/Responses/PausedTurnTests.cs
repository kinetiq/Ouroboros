using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.LargeLanguageModels.Providers;
using Ouroboros.LargeLanguageModels.Providers.Anthropic;
using Ouroboros.Responses;
using Ouroboros.Test.TestSupport;

namespace Ouroboros.Test.Responses;

/// <summary>
/// Continuing a turn the provider paused part-way through.
/// </summary>
/// <remarks>
/// Anthropic pauses a turn when its own server-side tool loop hits an internal limit. The turn is
/// neither finished nor failed, and before this it came back half-done and successful - the model
/// having run one round of tools and stopped mid-thought, with only StopReason to say so.
/// </remarks>
public class PausedTurnTests
{
    [Fact]
    public async Task A_Paused_Turn_Is_Continued_And_Comes_Back_Complete()
    {
        var transport = new SequencedTransport(
            SequencedTransport.Message("pause_turn", SequencedTransport.TextBlock("first half")),
            SequencedTransport.Message("end_turn", SequencedTransport.TextBlock("second half")));

        var response = await Send(transport);

        var success = Assert.IsType<OuroResponseSuccess>(response);

        Assert.Equal(2, transport.Calls);

        // One turn, so one response: everything it produced, not just the last round.
        Assert.Equal(OuroStopReason.EndTurn, success.StopReason);
        Assert.True(success.IsComplete);
        Assert.Contains("first half", success.ResponseText);
        Assert.Contains("second half", success.ResponseText);
        Assert.Equal(2, success.Content.Count);
    }

    /// <summary>
    /// The thinking signature survives into the continuation, byte for byte.
    /// </summary>
    /// <remarks>
    /// The assertion this whole feature turns on. Anthropic verifies the signature on a thinking
    /// block sent back to it, so a converter that rebuilt blocks field by field would have the
    /// entire continuation rejected the moment it dropped, reordered or re-encoded anything. Going
    /// through the block's own raw JSON is what makes that impossible rather than merely unlikely.
    /// </remarks>
    [Fact]
    public async Task The_Continuation_Sends_Back_The_Thinking_Signature_Unchanged()
    {
        const string signature = "ErUBCkYIBRgCIkD+xyz/SIGNATURE==";

        var transport = new SequencedTransport(
            SequencedTransport.Message("pause_turn", SequencedTransport.ThinkingAndText(signature, "partial")),
            SequencedTransport.Message("end_turn", SequencedTransport.TextBlock("done")));

        await Send(transport);

        Assert.Equal(2, transport.Calls);

        // Parsed rather than substring-matched. A real signature is base64 and contains '+' and
        // '/', which System.Text.Json escapes on the way out - so the bytes on the wire correctly
        // differ from the literal, and only decoding compares the values the API will actually see.
        using var continuation = JsonDocument.Parse(transport.Requests[1]);

        var turns = continuation.RootElement.GetProperty("messages");

        // The user's question, then the assistant turn carrying what the model already produced,
        // which is the shape the API expects a paused turn to be handed back as.
        Assert.Equal(2, turns.GetArrayLength());

        var assistant = turns[1];

        Assert.Equal("assistant", assistant.GetProperty("role").GetString());

        var thinking = assistant.GetProperty("content")[0];

        Assert.Equal("thinking", thinking.GetProperty("type").GetString());
        Assert.Equal(signature, thinking.GetProperty("signature").GetString());

        // And the first request carried no assistant turn at all, or the continuation is not what
        // put one there.
        using var first = JsonDocument.Parse(transport.Requests[0]);

        Assert.Equal(1, first.RootElement.GetProperty("messages").GetArrayLength());
    }

    /// <summary>
    /// Token usage is summed across the whole turn, not taken from the last request.
    /// </summary>
    /// <remarks>
    /// Each request bills its own input, and a continuation re-sends everything produced so far -
    /// so the later requests are the expensive ones. Reporting only the last would under-bill every
    /// paused turn, silently and in the provider's favour.
    /// </remarks>
    [Fact]
    public async Task Usage_Is_Summed_Across_Every_Request_The_Turn_Took()
    {
        var transport = new SequencedTransport(
            SequencedTransport.Message("pause_turn", SequencedTransport.TextBlock("a"), inputTokens: 100, outputTokens: 20),
            SequencedTransport.Message("end_turn", SequencedTransport.TextBlock("b"), inputTokens: 130, outputTokens: 15));

        var success = Assert.IsType<OuroResponseSuccess>(await Send(transport));

        Assert.Equal(230, success.PromptTokens);
        Assert.Equal(35, success.CompletionTokens);
        Assert.Equal(265, success.TotalTokenUsage);
    }

    /// <summary>
    /// A turn that keeps pausing stops at the budget and says so.
    /// </summary>
    /// <remarks>
    /// Not an error - the response is real and successful, just unfinished. IsComplete reports it
    /// as incomplete, which is the same signal a truncated response gives, and for the same reason:
    /// the caller decides what an unfinished answer is worth.
    /// </remarks>
    [Fact]
    public async Task A_Turn_That_Keeps_Pausing_Stops_At_The_Budget()
    {
        var alwaysPaused = Enumerable
            .Range(0, Constants.MaxPausedTurnContinuations + 1)
            .Select(index => SequencedTransport.Message("pause_turn", SequencedTransport.TextBlock($"round {index}")))
            .ToArray();

        var transport = new SequencedTransport(alwaysPaused);

        var success = Assert.IsType<OuroResponseSuccess>(await Send(transport));

        // The first request plus the continuations the budget allows, and not one more.
        Assert.Equal(Constants.MaxPausedTurnContinuations + 1, transport.Calls);

        Assert.Equal(OuroStopReason.Paused, success.StopReason);
        Assert.False(success.IsComplete);

        // Still a success carrying everything it managed - the caller is not left with nothing.
        Assert.True(success.Success);
        Assert.Equal(Constants.MaxPausedTurnContinuations + 1, success.Content.Count);
    }

    /// <summary>
    /// A turn that ends on the first response sends exactly one request.
    /// </summary>
    [Fact]
    public async Task An_Unpaused_Turn_Sends_One_Request()
    {
        var transport = new SequencedTransport(
            SequencedTransport.Message("end_turn", SequencedTransport.TextBlock("all done")));

        var success = Assert.IsType<OuroResponseSuccess>(await Send(transport));

        Assert.Equal(1, transport.Calls);
        Assert.Equal("all done", success.ResponseText);
        Assert.Equal(OuroStopReason.EndTurn, success.StopReason);
    }

    private static async Task<OuroResponseBase> Send(SequencedTransport transport)
    {
        var provider = new AnthropicChatProvider(transport.ToAnthropicClient());

        var attempt = await provider.SendAsync(
            [OuroMessage.FromUser("run something long")],
            new ChatOptions { Model = OuroModels.Claude_Opus_5 },
            CancellationToken.None);

        return attempt.Response;
    }
}
