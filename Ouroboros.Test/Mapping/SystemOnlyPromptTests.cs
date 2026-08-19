using System.Collections.Generic;
using System.Linq;
using OpenAI.Responses;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.LargeLanguageModels.Providers;
using Ouroboros.LargeLanguageModels.Providers.OpenAi;

namespace Ouroboros.Test.Mapping;

/// <summary>
/// Covers conversations with no user or assistant turn, and the empty conversation.
/// </summary>
/// <remarks>
/// The shape matters because the Responses API requires 'input', and the mapper's Instructions
/// lift normally empties the message list into it. A system-only conversation left the request
/// with no input at all, which OpenAI rejected as missing_required_parameter - the bug that took
/// out every consumer whose prompt had no user turn.
/// </remarks>
public class SystemOnlyPromptTests
{
    private static ChatOptions OpenAiOptions => new() { Model = OuroModels.Gpt_5_4_mini };

    /// <summary>
    /// With nothing else to put in 'input', the system prompt goes there as system-role items.
    /// </summary>
    [Fact]
    public void A_System_Only_List_Maps_To_System_Role_Input_On_OpenAi()
    {
        var mapped = OpenAiMappings.MapOptions(
            [
                OuroMessage.FromSystem("You are a judge."),
                OuroMessage.FromSystem("Grade the output below.")
            ],
            OpenAiOptions);

        Assert.Null(mapped.Instructions);

        var items = mapped.InputItems.Cast<MessageResponseItem>().ToList();

        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.Equal(MessageRole.System, item.Role));

        // Order is content: instructions read in the sequence they were written.
        Assert.Equal("You are a judge.", items[0].Content[0].Text);
        Assert.Equal("Grade the output below.", items[1].Content[0].Text);
    }

    /// <summary>
    /// One non-system message is enough to keep the normal shape: system prompts lift to
    /// Instructions and only the conversation rides in 'input'.
    /// </summary>
    [Fact]
    public void A_Mixed_List_Keeps_The_Instructions_Lift_On_OpenAi()
    {
        var mapped = OpenAiMappings.MapOptions(
            [
                OuroMessage.FromSystem("You are a judge."),
                OuroMessage.FromUser("Grade this.")
            ],
            OpenAiOptions);

        Assert.Equal("You are a judge.", mapped.Instructions);

        var item = Assert.IsAssignableFrom<MessageResponseItem>(Assert.Single(mapped.InputItems));

        Assert.Equal(MessageRole.User, item.Role);
    }

    /// <summary>
    /// A system-only call is fine on OpenAI, so pre-flight must let it through.
    /// </summary>
    [Fact]
    public void A_System_Only_List_Is_Supported_On_OpenAi()
    {
        var check = ProviderCapabilities.Check(
            [OuroMessage.FromSystem("You are a judge.")], OpenAiOptions, OuroProvider.OpenAi);

        Assert.True(check.IsSupported);
    }

    /// <summary>
    /// Anthropic cannot express the shape at all, so it is refused before a call is spent,
    /// with a message naming the fix rather than the vendor's wire error.
    /// </summary>
    [Fact]
    public void A_System_Only_List_Is_Not_Servable_On_Anthropic()
    {
        var check = ProviderCapabilities.Check(
            [OuroMessage.FromSystem("You are a judge.")],
            new ChatOptions { Model = OuroModels.Claude_Opus_5 },
            OuroProvider.Anthropic);

        Assert.Equal(CapabilityVerdict.NotServable, check.Verdict);
        Assert.Contains("user", check.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// An empty conversation asks nothing, whoever would serve it.
    /// </summary>
    [Theory]
    [InlineData(OuroProvider.OpenAi)]
    [InlineData(OuroProvider.Anthropic)]
    public void An_Empty_List_Is_Invalid_On_Any_Provider(OuroProvider provider)
    {
        var check = ProviderCapabilities.Check(new List<OuroMessage>(), OpenAiOptions, provider);

        Assert.Equal(CapabilityVerdict.Invalid, check.Verdict);
    }
}
