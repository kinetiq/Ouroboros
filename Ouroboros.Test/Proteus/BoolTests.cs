using Ouroboros.TextProcessing;

namespace Ouroboros.Test.Proteus;

public class CodexTests
{
    [Fact]
    public void True_Works()
    {
        Assert.True(ProteusConvert.ToBool("y"));
        Assert.True(ProteusConvert.ToBool("Y"));
        Assert.True(ProteusConvert.ToBool("true"));
    }

    [Fact]
    public void False_Works()
    {
        Assert.False(ProteusConvert.ToBool("n"));
        Assert.False(ProteusConvert.ToBool("N"));
        Assert.False(ProteusConvert.ToBool("false"));
    }
}