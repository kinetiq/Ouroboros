using Ouroboros.Extensions;
using Ouroboros.LargeLanguageModels;

namespace Ouroboros.Test.Models;

public class ModelAttributeTests
{
    [Fact]
    public void GetMaxTokens_Returns_Correct_Value()
    {
        var maxTokens = OuroModels.TextDavinciV2.GetMaxTokens();

        Assert.Equal(2048, maxTokens);
    }
}