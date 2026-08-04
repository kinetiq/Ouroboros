using System.Collections.Generic;
using System.Threading.Tasks;
using Betalgo.Ranul.OpenAI.Managers;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.Responses;
using Ouroboros.Tracking;

namespace Ouroboros.Test.Tracking;

/// <summary>
/// Covers what the OnChatCompleted hook receives. The Model member in particular is what lets a
/// consumer pick the right tokenizer, so it has to carry the model that actually ran.
/// </summary>
public class ChatCompletedArgsTests
{
    [Fact]
    public async Task Hook_Receives_The_Explicitly_Requested_Model()
    {
        var (client, _) = BuildClient();
        ChatCompletedArgs? captured = null;
        client.OnChatCompleted = args => { captured = args; return Task.CompletedTask; };

        await client.ChatAsync([OuroMessage.FromUser("hi")], new ChatOptions { Model = OuroModels.Gpt_5_5 });

        Assert.NotNull(captured);
        Assert.Equal(OuroModels.Gpt_5_5, captured!.Model);
    }

    /// <summary>
    /// The case a future refactor breaks silently: with no model on the options, the hook must
    /// still report the model the request actually ran on, not null or default.
    /// </summary>
    [Fact]
    public async Task Hook_Receives_The_Resolved_Default_When_No_Model_Was_Set()
    {
        var (client, _) = BuildClient();
        ChatCompletedArgs? captured = null;
        client.OnChatCompleted = args => { captured = args; return Task.CompletedTask; };

        await client.ChatAsync([OuroMessage.FromUser("hi")]);

        Assert.NotNull(captured);
        Assert.Equal(Constants.DefaultChatModel, captured!.Model);
    }

    [Fact]
    public async Task Hook_Receives_The_Model_Set_Via_SetDefaultChatModel()
    {
        var (client, _) = BuildClient();
        client.SetDefaultChatModel(OuroModels.Gpt_5_nano, OuroReasoningEffort.High);

        ChatCompletedArgs? captured = null;
        client.OnChatCompleted = args => { captured = args; return Task.CompletedTask; };

        await client.ChatAsync([OuroMessage.FromUser("hi")]);

        Assert.NotNull(captured);
        Assert.Equal(OuroModels.Gpt_5_nano, captured!.Model);
        Assert.Equal(OuroReasoningEffort.High, captured.ReasoningEffort);
    }

    [Fact]
    public async Task Hook_Receives_The_Messages_That_Were_Sent()
    {
        var (client, _) = BuildClient();
        ChatCompletedArgs? captured = null;
        client.OnChatCompleted = args => { captured = args; return Task.CompletedTask; };

        List<OuroMessage> messages = [OuroMessage.FromSystem("sys"), OuroMessage.FromUser("usr")];
        await client.ChatAsync(messages);

        Assert.NotNull(captured);
        Assert.Equal(2, captured!.Messages.Count);
        Assert.Equal(OuroRole.System, captured.Messages[0].Role);
        Assert.Equal("usr", captured.Messages[1].Content);
    }

    private static (OuroClient Client, StubChatRequestHandler Handler) BuildClient()
    {
        var handler = new StubChatRequestHandler();

        return (new OuroClient("test-key", handler), handler);
    }

    /// <summary>
    /// Returns a canned response so OuroClient can be exercised without a network call.
    /// </summary>
    private class StubChatRequestHandler() : ChatRequestHandler(null)
    {
        public override Task<OuroResponseBase> CompleteAsync(List<OuroMessage> messages, OpenAIService api,
            ChatOptions? options = null)
        {
            return Task.FromResult<OuroResponseBase>(new OuroResponseSuccess("stubbed")
            {
                Model = "stub",
                PromptTokens = 11,
                CompletionTokens = 7,
                TotalTokenUsage = 18
            });
        }
    }
}
