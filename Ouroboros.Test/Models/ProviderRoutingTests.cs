using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ouroboros.Config;
using Ouroboros.Core;
using Ouroboros.Extensions;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;

namespace Ouroboros.Test.Models;

/// <summary>
/// Covers how a model gets routed to a provider, and what happens when it cannot be.
/// </summary>
public class ProviderRoutingTests
{
    /// <summary>
    /// The attribute is what routes a request, so a model without one is unusable. Reflection means
    /// there is no switch to forget to update - but there is still an attribute to forget to add,
    /// and this is what catches that.
    /// </summary>
    [Fact]
    public void Every_Model_Declares_A_Provider()
    {
        foreach (var model in Enum.GetValues<OuroModels>())
        {
            var provider = model.GetProvider();

            Assert.True(Enum.IsDefined(provider), $"{model} maps to an undefined provider.");
        }
    }

    [Theory]
    [InlineData(OuroModels.Gpt_5_4_mini, OuroProvider.OpenAi)]
    [InlineData(OuroModels.Gpt_5_5, OuroProvider.OpenAi)]
    [InlineData(OuroModels.Claude_Opus_5, OuroProvider.Anthropic)]
    [InlineData(OuroModels.Claude_Haiku_4_5, OuroProvider.Anthropic)]
    public void Models_Route_To_The_Expected_Provider(OuroModels model, OuroProvider expected)
    {
        Assert.Equal(expected, model.GetProvider());
    }

    /// <summary>
    /// The failure a caller is most likely to hit first: adding a Claude model to an app that only
    /// ever had an OpenAI key. It has to name the missing provider, or the error reads as an
    /// authentication problem with the vendor they thought they were calling.
    /// </summary>
    [Fact]
    public async Task A_Claude_Model_Without_An_Anthropic_Key_Fails_Naming_Anthropic()
    {
        using var client = new OuroClient(new OuroborosOptions { OpenAiApiKey = "openai-only" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.ChatAsync(
            [OuroMessage.FromUser("hi")],
            new ChatOptions { Model = OuroModels.Claude_Opus_5 }));

        Assert.Contains("Anthropic", ex.Message);
    }

    [Fact]
    public async Task A_Gpt_Model_Without_An_OpenAi_Key_Fails_Naming_OpenAi()
    {
        using var client = new OuroClient(new OuroborosOptions { AnthropicApiKey = "anthropic-only" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.ChatAsync(
            [OuroMessage.FromUser("hi")],
            new ChatOptions { Model = OuroModels.Gpt_5_4_mini }));

        Assert.Contains("OpenAi", ex.Message);
    }

    /// <summary>
    /// The single-key constructor predates multi-provider and still means OpenAI, so existing
    /// callers keep working untouched.
    /// </summary>
    [Fact]
    public async Task The_Legacy_Single_Key_Constructor_Still_Means_OpenAi()
    {
        using var client = new OuroClient("openai-only");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.ChatAsync(
            [OuroMessage.FromUser("hi")],
            new ChatOptions { Model = OuroModels.Claude_Opus_5 }));

        Assert.Contains("Anthropic", ex.Message);
    }

    /// <summary>
    /// Nothing should be constructed until a call actually needs it - a client configured for one
    /// provider must not fail at construction because the other has no key.
    /// </summary>
    [Fact]
    public void Constructing_With_Only_One_Key_Does_Not_Throw()
    {
        using var openAiOnly = new OuroClient(new OuroborosOptions { OpenAiApiKey = "key" });
        using var anthropicOnly = new OuroClient(new OuroborosOptions { AnthropicApiKey = "key" });

        Assert.NotNull(openAiOnly);
        Assert.NotNull(anthropicOnly);
    }
}
