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
/// neither finished nor failed. Before this it came back half-done and successful: one round of
/// tools run, stopped mid-thought, with only StopReason to say so.
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

    /// <summary>
    /// A tool result arriving after the pause still finds the invocation it belongs to.
    /// </summary>
    /// <remarks>
    /// The correlation map used to be rebuilt each round. A pause falling between a server_tool_use
    /// and its result then left an execution that apparently never ran, plus a result belonging to
    /// nothing. The file-editor handling fixed that same orphaning once already.
    /// </remarks>
    [Fact]
    public async Task A_Tool_Result_Arriving_After_A_Pause_Finds_Its_Invocation()
    {
        const string toolUseId = "srvtoolu_split";

        var transport = new SequencedTransport(
            SequencedTransport.Message("pause_turn", SequencedTransport.ToolUseBlock(toolUseId, "echo hi")),
            SequencedTransport.Message("end_turn", SequencedTransport.ToolResultBlock(toolUseId, "hi")));

        var success = Assert.IsType<OuroResponseSuccess>(await Send(transport));

        // One execution, not an orphaned invocation plus a homeless result.
        var execution = Assert.Single(success.CodeExecutions);

        Assert.NotNull(execution.Result);
        Assert.Equal("hi", execution.Result!.Stdout);
        Assert.Equal(0, execution.Result.ExitCode);
        Assert.False(execution.Result.IsToolError);

        // And the code the model asked to run is still on it, from the earlier round.
        Assert.Contains("echo hi", execution.Code);
    }

    /// <summary>
    /// A continuation asks only for what is left of the caller's token ceiling.
    /// </summary>
    /// <remarks>
    /// max_tokens is per request. Handing the full ceiling to every round would let a turn that
    /// paused three times produce four times what the caller asked for, and bill for it.
    /// </remarks>
    [Fact]
    public async Task A_Continuation_Asks_Only_For_The_Remaining_Tokens()
    {
        var transport = new SequencedTransport(
            SequencedTransport.Message("pause_turn", SequencedTransport.TextBlock("a"), outputTokens: 300),
            SequencedTransport.Message("end_turn", SequencedTransport.TextBlock("b"), outputTokens: 50));

        await Send(transport, maxCompletionTokens: 1000);

        Assert.Equal(2, transport.Calls);

        Assert.Equal(1000, MaxTokensOf(transport.Requests[0]));
        Assert.Equal(700, MaxTokensOf(transport.Requests[1]));
    }

    /// <summary>
    /// A turn that spends the whole ceiling in one round is not continued.
    /// </summary>
    [Fact]
    public async Task A_Spent_Token_Budget_Ends_The_Turn()
    {
        var transport = new SequencedTransport(
            SequencedTransport.Message("pause_turn", SequencedTransport.TextBlock("all of it"), outputTokens: 1000));

        var success = Assert.IsType<OuroResponseSuccess>(await Send(transport, maxCompletionTokens: 1000));

        // Continuing would have asked for zero more tokens, which is not a request worth making.
        Assert.Equal(1, transport.Calls);
        Assert.Equal(OuroStopReason.Paused, success.StopReason);
        Assert.False(success.IsComplete);
    }

    /// <summary>
    /// With no ceiling set, a continuation is capped by the model rather than by arithmetic.
    /// </summary>
    [Fact]
    public async Task Without_A_Ceiling_A_Continuation_Uses_The_Model_Maximum()
    {
        var transport = new SequencedTransport(
            SequencedTransport.Message("pause_turn", SequencedTransport.TextBlock("a"), outputTokens: 300),
            SequencedTransport.Message("end_turn", SequencedTransport.TextBlock("b")));

        await Send(transport);

        Assert.Equal(2, transport.Calls);

        // The same value both times: nothing was subtracted, because nothing was asked for.
        Assert.Equal(MaxTokensOf(transport.Requests[0]), MaxTokensOf(transport.Requests[1]));
    }

    private static long MaxTokensOf(string requestBody)
    {
        using var request = JsonDocument.Parse(requestBody);

        return request.RootElement.GetProperty("max_tokens").GetInt64();
    }

    private static Task<OuroResponseBase> Send(SequencedTransport transport)
    {
        return Send(transport, null);
    }

    private static async Task<OuroResponseBase> Send(SequencedTransport transport, int? maxCompletionTokens)
    {
        var provider = new AnthropicChatProvider(transport.ToAnthropicClient());

        var attempt = await provider.SendAsync(
            [OuroMessage.FromUser("run something long")],
            new ChatOptions
            {
                Model = OuroModels.Claude_Opus_5,
                MaxCompletionTokens = maxCompletionTokens
            },
            CancellationToken.None);

        return attempt.Response;
    }
}
