using System.Threading.Tasks;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.LargeLanguageModels.Providers;
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
        var response = await Send(StubTransport.ChatCompletion("  Hello there.  "));

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
        var response = await Send(StubTransport.ChatCompletion("done"));

        var success = Assert.IsType<OuroResponseSuccess>(response);

        Assert.Equal(OuroStopReason.EndTurn, success.StopReason);
        Assert.True(success.IsComplete);
    }

    /// <summary>
    /// The latent bug the stop reason fixes: before this, a response truncated at the token ceiling
    /// was indistinguishable from a complete one. Truncated structured output just fails to parse
    /// and leaves a null ResponseObject on an otherwise successful response.
    /// </summary>
    [Fact]
    public async Task A_Truncated_Completion_Is_Reported_As_Incomplete()
    {
        var response = await Send(StubTransport.ChatCompletion("cut off mid-", finishReason: "length"));

        var success = Assert.IsType<OuroResponseSuccess>(response);

        Assert.Equal(OuroStopReason.MaxTokens, success.StopReason);
        Assert.False(success.IsComplete);

        // Still a success - it is truncated, not failed. The caller decides what to do about it.
        Assert.True(success.Success);
    }

    [Fact]
    public async Task An_Unrecognised_Finish_Reason_Degrades_To_Unknown()
    {
        var response = await Send(StubTransport.ChatCompletion("x", finishReason: "some_new_reason"));

        var success = Assert.IsType<OuroResponseSuccess>(response);

        Assert.Equal(OuroStopReason.Unknown, success.StopReason);

        // Unknown is not a truncation signal, so the response still counts as complete.
        Assert.True(success.IsComplete);
    }

    private static Task<OuroResponseBase> Send(string responseJson)
    {
        return new ChatExecutor().ExecuteAsync(
            new OpenAiChatProvider(new StubTransport(responseJson).ToApi()),
            [OuroMessage.FromUser("hi")],
            new ChatOptions { Model = OuroModels.Gpt_5_4_mini });
    }
}
