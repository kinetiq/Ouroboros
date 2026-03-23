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

    [Fact]
    public void Instruct_Model_Has_Completion_Max_Tokens()
    {
        var maxTokens = OuroModels.Gpt_3_5_Turbo_Instruct.GetMaxOutputTokens();

        Assert.Equal(4096, maxTokens);
    }

    [Fact]
    public void Default_Completion_Model_Is_Gpt_3_5_Turbo_Instruct()
    {
        Assert.Equal(OuroModels.Gpt_3_5_Turbo_Instruct, Constants.DefaultCompletionModel);
    }
}