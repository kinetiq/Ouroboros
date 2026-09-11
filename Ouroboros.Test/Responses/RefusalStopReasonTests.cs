using System.Threading;
using System.Threading.Tasks;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.LargeLanguageModels.Providers.Anthropic;
using Ouroboros.Responses;
using Ouroboros.Test.TestSupport;

namespace Ouroboros.Test.Responses;

/// <summary>
/// A refused request comes back as HTTP 200 with an empty or partial answer, so nothing about the
/// transport says anything went wrong.
/// </summary>
/// <remarks>
/// Without its own stop reason, "refusal" landed on Unknown, and IsComplete counts Unknown as
/// complete. A caller saw a finished response that happened to say nothing, which is the one
/// failure mode StopReason exists to prevent.
/// </remarks>
public class RefusalStopReasonTests
{
    [Fact]
    public async Task A_Refusal_Is_Reported_As_A_Refusal()
    {
        var response = await Send(SequencedTransport.Message("refusal", "[]"));

        var success = Assert.IsType<OuroResponseSuccess>(response);
        Assert.Equal(OuroStopReason.Refusal, success.StopReason);
    }

    [Fact]
    public async Task A_Refusal_Is_Not_Complete()
    {
        var response = await Send(SequencedTransport.Message("refusal", "[]"));

        var success = Assert.IsType<OuroResponseSuccess>(response);
        Assert.False(success.IsComplete);
    }

    /// <summary>
    /// The continuation loop runs on Paused alone. A refusal has to end the turn rather than be
    /// resent, which would spend the whole continuation budget on a request already declined.
    /// </summary>
    [Fact]
    public async Task A_Refusal_Is_Not_Continued()
    {
        var transport = new SequencedTransport(SequencedTransport.Message("refusal", "[]"));

        await Send(transport);

        Assert.Equal(1, transport.Calls);
    }

    private static Task<OuroResponseBase> Send(string cannedResponse)
    {
        return Send(new SequencedTransport(cannedResponse));
    }

    private static async Task<OuroResponseBase> Send(SequencedTransport transport)
    {
        var provider = new AnthropicChatProvider(transport.ToAnthropicClient());

        var attempt = await provider.SendAsync(
            [OuroMessage.FromUser("something declined")],
            new ChatOptions { Model = OuroModels.Claude_Fable_5_1 },
            CancellationToken.None);

        return attempt.Response;
    }
}
