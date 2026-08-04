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
    public void All_Supported_Models_Have_An_Encoding()
    {
        foreach (var model in Enum.GetValues<OuroModels>())
        {
            var tokens = OuroClient.TokenCount("hello", model);

            Assert.True(tokens > 0, $"No tokenizer encoding mapped for {model}.");
        }
    }
}
