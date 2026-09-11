using System.Collections.Generic;
using System.Linq;
using Ouroboros.Core;
using Ouroboros.Extensions;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.LargeLanguageModels.Providers.Anthropic;

namespace Ouroboros.Test.Mapping;

/// <summary>
/// Covers where Anthropic's request shape diverges from OpenAI's. These are the differences that
/// cause silent misbehaviour rather than a compile error, so they are worth pinning individually.
/// </summary>
public class AnthropicMappingsTests
{
    /// <summary>
    /// The big structural difference: Anthropic takes the system prompt as a top-level field, not
    /// as a message with a system role. Left in the message list it would be sent as a user turn.
    /// </summary>
    [Fact]
    public void The_System_Message_Is_Lifted_Out_Of_The_Message_List()
    {
        var mapped = Map([
            OuroMessage.FromSystem("You are terse."),
            OuroMessage.FromUser("Hello.")
        ]);

        Assert.Equal("You are terse.", SystemText(mapped));
        Assert.Single(mapped.Messages);
    }

    /// <summary>
    /// Ouroboros permits several system messages. Keeping only the first would silently drop
    /// instructions - which surfaces later as the model ignoring a rule nobody can find.
    /// </summary>
    [Fact]
    public void Multiple_System_Messages_Are_Joined_Rather_Than_Dropped()
    {
        var mapped = Map([
            OuroMessage.FromSystem("Rule one."),
            OuroMessage.FromSystem("Rule two."),
            OuroMessage.FromUser("Hello.")
        ]);

        var system = SystemText(mapped);

        Assert.Contains("Rule one.", system);
        Assert.Contains("Rule two.", system);
    }

    [Fact]
    public void A_Conversation_With_No_System_Message_Sends_None()
    {
        var mapped = Map([OuroMessage.FromUser("Hello.")]);

        Assert.Null(mapped.System);
    }

    [Fact]
    public void User_And_Assistant_Turns_Keep_Their_Order_And_Roles()
    {
        var mapped = Map([
            OuroMessage.FromUser("First."),
            OuroMessage.FromAssistant("Second."),
            OuroMessage.FromUser("Third.")
        ]);

        Assert.Equal(3, mapped.Messages.Count);
        Assert.Equal("user", (string?)mapped.Messages[0].Role);
        Assert.Equal("assistant", (string?)mapped.Messages[1].Role);
        Assert.Equal("user", (string?)mapped.Messages[2].Role);
    }

    /// <summary>
    /// MaxTokens is required by Anthropic and optional on OpenAI, so an unset value has to become
    /// something rather than being omitted.
    /// </summary>
    [Fact]
    public void MaxTokens_Defaults_To_The_Model_Ceiling_When_Unset()
    {
        var mapped = Map([OuroMessage.FromUser("hi")], new ChatOptions { Model = OuroModels.Claude_Opus_5 });

        Assert.Equal(OuroModels.Claude_Opus_5.GetMaxOutputTokens(), mapped.MaxTokens);
    }

    [Fact]
    public void An_Explicit_MaxCompletionTokens_Wins()
    {
        var mapped = Map([OuroMessage.FromUser("hi")], new ChatOptions
        {
            Model = OuroModels.Claude_Opus_5,
            MaxCompletionTokens = 1234
        });

        Assert.Equal(1234, mapped.MaxTokens);
    }

    /// <summary>
    /// Appending a date to a current Claude id produces a 404, so every id is pinned here.
    /// </summary>
    [Theory]
    [InlineData(OuroModels.Claude_Opus_5, "claude-opus-5")]
    [InlineData(OuroModels.Claude_Opus_4_8, "claude-opus-4-8")]
    [InlineData(OuroModels.Claude_Sonnet_5, "claude-sonnet-5")]
    [InlineData(OuroModels.Claude_Haiku_4_5, "claude-haiku-4-5")]
    [InlineData(OuroModels.Claude_Fable_5_1, "claude-fable-5-1")]
    [InlineData(OuroModels.Claude_Fable_5, "claude-fable-5")]
    public void The_Model_Id_Carries_No_Date_Suffix(OuroModels model, string expected)
    {
        var mapped = Map([OuroMessage.FromUser("hi")], new ChatOptions { Model = model });

        Assert.Equal(expected, (string?)mapped.Model);
    }

    /// <summary>
    /// Every level, not just one. XHigh and Max arrived after the other three and map to SDK
    /// members whose names do not match ours, so a wrong arm would send a level the caller never
    /// asked for rather than failing.
    /// </summary>
    [Theory]
    [InlineData(OuroReasoningEffort.Low, "low")]
    [InlineData(OuroReasoningEffort.Medium, "medium")]
    [InlineData(OuroReasoningEffort.High, "high")]
    [InlineData(OuroReasoningEffort.XHigh, "xhigh")]
    [InlineData(OuroReasoningEffort.Max, "max")]
    public void Reasoning_Effort_Is_Carried_As_Output_Config_Effort(OuroReasoningEffort level, string expected)
    {
        var mapped = Map([OuroMessage.FromUser("hi")], new ChatOptions
        {
            Model = OuroModels.Claude_Opus_5,
            ReasoningEffort = level
        });

        Assert.NotNull(mapped.OutputConfig);
        Assert.Equal(expected, mapped.OutputConfig!.Effort is { } effort ? (string?)effort : null);
    }

    [Fact]
    public void No_Reasoning_Effort_Means_No_Output_Config()
    {
        var mapped = Map([OuroMessage.FromUser("hi")], new ChatOptions
        {
            Model = OuroModels.Claude_Opus_5,
            ReasoningEffort = null
        });

        Assert.Null(mapped.OutputConfig);
    }

    /// <summary>
    /// Sampling parameters were removed from ChatOptions in 5.0, which happens to be exactly what
    /// current Claude models require - they reject temperature, top_p and top_k outright.
    /// </summary>
    /// <remarks>
    /// The SDK marks all three obsolete for the same reason, so reading them here warns. Suppressed
    /// deliberately and narrowly: reading them is the entire point of the assertion, and the test
    /// guards against anyone reintroducing them to ChatOptions and wiring them through.
    /// </remarks>
    [Fact]
    public void No_Sampling_Parameters_Are_Sent()
    {
        var mapped = Map([OuroMessage.FromUser("hi")]);

#pragma warning disable CS0618
        Assert.Null(mapped.Temperature);
        Assert.Null(mapped.TopP);
        Assert.Null(mapped.TopK);
#pragma warning restore CS0618
    }

    [Fact]
    public void Stop_Sequences_Are_Passed_Through()
    {
        var mapped = Map([OuroMessage.FromUser("hi")], new ChatOptions
        {
            Model = OuroModels.Claude_Opus_5,
            StopSequences = ["END"]
        });

        Assert.Equal(["END"], mapped.StopSequences);
    }

    /// <summary>
    /// The system field is a union of a plain string or a list of text blocks. ToString() on these
    /// SDK wrappers emits JSON - quotation marks and all - so narrow the union instead.
    /// </summary>
    private static string SystemText(Anthropic.Models.Messages.MessageCreateParams mapped)
    {
        Assert.NotNull(mapped.System);
        Assert.True(mapped.System!.TryPickString(out var text), "System was not sent as a plain string.");

        return text ?? "";
    }

    private static Anthropic.Models.Messages.MessageCreateParams Map(List<OuroMessage> messages,
        ChatOptions? options = null)
    {
        return AnthropicMappings.MapOptions(messages, options ?? new ChatOptions
        {
            Model = OuroModels.Claude_Opus_5
        });
    }
}
