using Ouroboros.Extensions;
using Ouroboros.LargeLanguageModels;

namespace Ouroboros.Test.Tokens;

public class TokenizerTests
{
    [Fact]
    public void Simple_Tokenizer()
    {
        var tokens = OuroClient.TokenCount("This is a test.");

        Assert.Equal(5, tokens);
    }
}