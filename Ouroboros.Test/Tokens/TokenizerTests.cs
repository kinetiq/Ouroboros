using System.Collections.Generic;
using System.Linq;
using Ouroboros.Extensions;
using Ouroboros.LargeLanguageModels;

namespace Ouroboros.Test.Tokens;

public class TokenizerTests
{
    [Fact]
    public void Simple_Tokenizer()
    {
        var tokens = OuroClient.TokenCount("This is a test.", OuroModels.Gpt_5_4_mini);

        Assert.Equal(5, tokens);
    }

    [Fact]
    public void Empty_String_Is_Zero_Tokens()
    {
        Assert.Equal(0, OuroClient.TokenCount("", OuroModels.Gpt_5_4_mini));
    }

    [Fact]
    public void Every_OpenAi_Model_Has_An_Encoding()
    {
        foreach (var model in ModelsFor(OuroProvider.OpenAi))
        {
            var tokens = OuroClient.TokenCount("hello", model);

            Assert.True(tokens > 0, $"No tokenizer encoding mapped for {model}.");
        }
    }

    /// <summary>
    /// Anthropic publishes no tokenizer, so there is no honest local count to give. Refusing is the
    /// deliberate behaviour - an approximation that is quietly some percent out is worse than a
    /// failure, because these numbers get persisted and costed against.
    /// </summary>
    [Fact]
    public void Anthropic_Models_Refuse_Rather_Than_Approximating()
    {
        foreach (var model in ModelsFor(OuroProvider.Anthropic))
        {
            var ex = Assert.Throws<NotSupportedException>(() => OuroClient.TokenCount("hello", model));

            // The message has to point at the alternative, or the reader's only move is to guess.
            Assert.Contains("PromptTokens", ex.Message);
        }
    }

    /// <summary>
    /// Guards the assumption both tests above rest on: that each provider actually has models in
    /// the enum, so neither is silently iterating an empty set.
    /// </summary>
    [Theory]
    [InlineData(OuroProvider.OpenAi)]
    [InlineData(OuroProvider.Anthropic)]
    public void Each_Provider_Has_At_Least_One_Model(OuroProvider provider)
    {
        Assert.NotEmpty(ModelsFor(provider));
    }

    private static List<OuroModels> ModelsFor(OuroProvider provider)
    {
        return Enum.GetValues<OuroModels>()
            .Where(model => model.GetProvider() == provider)
            .ToList();
    }
}
