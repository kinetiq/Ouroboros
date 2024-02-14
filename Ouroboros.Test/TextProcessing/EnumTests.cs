using Ouroboros.Enums;
using Ouroboros.Extensions;
using Ouroboros.Responses;

namespace Ouroboros.Test.TextProcessing;

public class EnumExtractorTests
{
    [Fact]
    public void Basic_Enum_Extraction_Works()
    {
        var response = new OuroResponseSuccess("   yes");

        Assert.Equal(YesNo.Yes, response.ExtractEnum<YesNo>());
    }

    [Fact]
    public void YesNo_Extraction_Works()
    {
        var response = new OuroResponseSuccess("no");

        Assert.Equal(YesNo.No, response.ExtractYesNo());
    }

    [Fact]
    public void MoMatch_State_Extraction_Works1()
    {
        var response = new OuroResponseSuccess("nerp");

        Assert.Equal(YesNo.NoMatch, response.ExtractYesNo());
    }

    [Fact]
    public void MoMatch_State_Extraction_Works2()
    {
        var response = new OuroResponseSuccess("");

        Assert.Equal(YesNo.NoMatch, response.ExtractYesNo());
    }
}