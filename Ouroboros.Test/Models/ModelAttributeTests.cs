using Ouroboros.Extensions;
using Ouroboros.LargeLanguageModels;

namespace Ouroboros.Test.Models;

public class ModelAttributeTests
{
    [Fact]
    public void GetMaxTokens_Returns_Correct_Value()
    {
        var maxTokens = OuroModels.Gpt_5_2.GetMaxOutputTokens();

        Assert.Equal(128000, maxTokens);
    }

    [Fact]
    public void Default_Chat_Model_Is_Gpt_6_Luna()
    {
        Assert.Equal(OuroModels.Gpt_6_Luna, Constants.DefaultChatModel);
    }
}
