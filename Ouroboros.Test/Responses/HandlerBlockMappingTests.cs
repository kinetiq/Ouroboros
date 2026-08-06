using System.Threading.Tasks;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.LargeLanguageModels.Providers;
using Ouroboros.LargeLanguageModels.Providers.OpenAi;
using Ouroboros.Responses;
using Ouroboros.Test.TestSupport;

namespace Ouroboros.Test.Responses;

/// <summary>
/// The block model as it comes out of the real request handler, rather than hand-assembled.
/// </summary>
public class HandlerBlockMappingTests
{
    [Fact]
    public async Task A_Chat_Completion_Maps_To_One_Text_Block()
    {
        var response = await Send(StubTransport.Response("  Hello there.  "));

        var success = Assert.IsType<OuroResponseSuccess>(response);
        var block = Assert.IsType<OuroTextBlock>(Assert.Single(success.Content));

        Assert.Equal("Hello there.", block.Text);

        // The block and the flat string must agree - trimming included, or a consumer reading one
        // and a consumer reading the other quietly disagree about whitespace.
        Assert.Equal(success.ResponseText, block.Text);
    }

    [Fact]
    public async Task A_Normal_Completion_Reports_EndTurn_And_Is_Complete()
    {
        var response = await Send(StubTransport.Response("done"));

        var success = Assert.IsType<OuroResponseSuccess>(response);

        Assert.Equal(OuroStopReason.EndTurn, success.StopReason);
        Assert.True(success.IsComplete);
    }

    /// <summary>
    /// The latent bug the stop reason fixes: before this, a response truncated at the token ceiling
    /// was indistinguishable from a complete one. Truncated structured output just fails to parse
    /// and leaves a null ResponseObject on an otherwise successful response.
    /// </summary>
    /// <remarks>
    /// The Responses API reports truncation as an incomplete status with a reason beside it, rather
    /// than as a finish reason on the message - so this is the shape the mapper has to read.
    /// </remarks>
    [Fact]
    public async Task A_Truncated_Completion_Is_Reported_As_Incomplete()
    {
        var response = await Send(StubTransport.Response(
            "cut off mid-", status: "incomplete", incompleteReason: "max_output_tokens"));

        var success = Assert.IsType<OuroResponseSuccess>(response);

        Assert.Equal(OuroStopReason.MaxTokens, success.StopReason);
        Assert.False(success.IsComplete);

        // Still a success - it is truncated, not failed. The caller decides what to do about it.
        Assert.True(success.Success);
    }

    /// <summary>
    /// An incomplete response that stopped for some reason other than the token ceiling.
    /// </summary>
    /// <remarks>
    /// Unknown rather than MaxTokens, deliberately: guessing truncation here would make a
    /// content-filtered response look like one the caller can fix by raising a limit.
    /// </remarks>
    [Fact]
    public async Task An_Unrecognised_Incomplete_Reason_Degrades_To_Unknown()
    {
        var response = await Send(StubTransport.Response(
            "x", status: "incomplete", incompleteReason: "content_filter"));

        var success = Assert.IsType<OuroResponseSuccess>(response);

        Assert.Equal(OuroStopReason.Unknown, success.StopReason);
    }

    private static Task<OuroResponseBase> Send(string responseJson)
    {
        return new ChatExecutor().ExecuteAsync(
            new OpenAiResponsesProvider(new StubTransport(responseJson).ToClient()),
            [OuroMessage.FromUser("hi")],
            new ChatOptions { Model = OuroModels.Gpt_5_4_mini });
    }
}
