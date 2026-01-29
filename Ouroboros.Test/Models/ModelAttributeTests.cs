using Ouroboros.Extensions;
using Ouroboros.LargeLanguageModels;

namespace Ouroboros.Test.Models;

public class TokenizerTests
{
    [Fact]
    public void GetMaxTokens_Returns_Correct_Value()
    {
        var maxTokens = OuroModels.Gpt_5_2.GetMaxOutputTokens();

        Assert.Equal(128000, maxTokens);
    }
}