using Ouroboros.Enums;
using Ouroboros.TextProcessing;

namespace Ouroboros.Test.Proteus;

public class EnumTests
{
    [Fact]
    public void Basic_ToEnum_Works()
    {
        Assert.Equal(BasicTestEnum.Value1, ProteusConvert.ToEnum<BasicTestEnum>("Value1"));
        Assert.Equal(BasicTestEnum.Value2, ProteusConvert.ToEnum<BasicTestEnum>("value2")); // case insensitive
        Assert.Equal(EnumWithEverything.Value2, ProteusConvert.ToEnum<EnumWithEverything>("Value2"));
    }

    [Fact]
    public void ToEnum_With_Alias()
    {
        Assert.Equal(EnumWithEverything.Value1, ProteusConvert.ToEnum<EnumWithEverything>("Value1")); // basic case
        Assert.Equal(EnumWithEverything.Value2, ProteusConvert.ToEnum<EnumWithEverything>("Cow")); // alias
        Assert.Equal(EnumWithEverything.Value2, ProteusConvert.ToEnum<EnumWithEverything>("COW")); // alias, case insensitive
    }

    [Fact]
    public void NoMatch_Works()
    {
        // Throws because this won't match and there's no NoMatch item (or attribute).
        Assert.Throws<NotImplementedException>(() => ProteusConvert.ToEnum<BasicTestEnum>("This value does not exist in the enum"));

        // Succeed because there's a NoMatch item.
        Assert.Equal(EnumWithNoMatch.NoMatch, ProteusConvert.ToEnum<EnumWithNoMatch>("This value does not exist in the enum"));

        // Succeeds because there's a NoMatch attribute.
        Assert.Equal(EnumWithEverything.Value3, ProteusConvert.ToEnum<EnumWithEverything>("This value does not exist in the enum"));
    }

    [Fact]
    public void Dynamic_Typing_Works()
    {
        // For certain cases where the type is not known at compile time, the type can be passed in as a parameter.
        Assert.Equal(BasicTestEnum.Value1, ProteusConvert.ToEnum(typeof(BasicTestEnum), "Value1"));
    }
}


public enum BasicTestEnum
{
    Value1,
    Value2
}

public enum EnumWithNoMatch
{
    Value1,
    Value2,

    /// <summary>
    /// NoMatch will be auto-discovered and selected if there isn't a match.
    /// </summary>
    NoMatch
}

public enum EnumWithEverything
{
    Value1,
    [Alias("Cow")] Value2,
    [NoMatch] Value3
}