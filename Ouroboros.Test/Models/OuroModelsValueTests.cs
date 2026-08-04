using Ouroboros.LargeLanguageModels;

namespace Ouroboros.Test.Models;

/// <summary>
/// OuroModels values are pinned explicitly. Keystone persists the enum *name*, but anything
/// storing the underlying int would silently remap if a member were removed and the rest
/// renumbered, so these assertions exist to make that a build failure instead.
/// </summary>
public class OuroModelsValueTests
{
    [Theory]
    [InlineData(OuroModels.Gpt_5, 1)]
    [InlineData(OuroModels.Gpt_5_1, 2)]
    [InlineData(OuroModels.Gpt_5_2, 3)]
    [InlineData(OuroModels.Gpt_5_mini, 4)]
    [InlineData(OuroModels.Gpt_5_nano, 5)]
    [InlineData(OuroModels.Gpt_5_4, 6)]
    [InlineData(OuroModels.Gpt_5_4_mini, 7)]
    [InlineData(OuroModels.Gpt_5_4_nano, 8)]
    [InlineData(OuroModels.Gpt_5_5, 9)]
    public void Model_Has_Its_Pinned_Value(OuroModels model, int expected)
    {
        Assert.Equal(expected, (int)model);
    }

    /// <summary>
    /// 0 was gpt-3.5-turbo-instruct, removed in 5.0. It is deliberately left undefined so that
    /// default(OuroModels) fails loudly rather than silently selecting a real model.
    /// </summary>
    [Fact]
    public void Zero_Is_Not_A_Defined_Model()
    {
        Assert.False(Enum.IsDefined((OuroModels)0));
    }
}
