using Ouroboros.Enums;
using Ouroboros.TextProcessing;

namespace Ouroboros.Test.Hermes;

public class CodexTests
{
    [Fact]
    public void Basic_Mapping_Works()
    {
        var codex = new HermeticCodex<ExampleModel1>();
        var model = codex.Bind(
@"Here's some intro text.

It will go on for a few lines.
## YesNo
yes

## SomeBool
True
## Description
This is a test description.");

        Assert.True(model.IsComplete());

        Assert.Equal(57, model.Intro.Length);
        Assert.Equal(YesNo.Yes, model.YesNo);
        Assert.True(model.SomeBool);
        Assert.Equal(27, model.Description.Length);
    }

    [Fact]
    public void Incomplete_Mapping_Detected()
    {
        var codex = new HermeticCodex<ExampleModel1>();
        var model = codex.Bind(
            @"Here's some intro text.

It will go on for a few lines.

## SomeBool
True
## Description
This is a test description.");

        Assert.False(model.IsComplete());

        Assert.Equal(57, model.Intro.Length);
        Assert.Equal(YesNo.NoMatch, model.YesNo);
        Assert.True(model.SomeBool);
        Assert.Equal(27, model.Description.Length);
    }

    [Fact]
    public void No_Intro_Works()
    {
        var codex = new HermeticCodex<ExampleModel2>();
        var model = codex.Bind(
@"## YesNo
N
## Description
This is a test description.");

        Assert.True(model.IsComplete());

        Assert.Equal(YesNo.No, model.YesNo);
    }
}

public class ExampleModel1 : CodexModel
{
    public YesNo YesNo { get; set; } = YesNo.NoMatch;
    public bool SomeBool { get; set; } = false;
    public string Description { get; set; } = string.Empty;
    public string Intro { get; set; } = string.Empty;
}


public class ExampleModel2 : CodexModel
{
    public YesNo YesNo { get; set; } = YesNo.NoMatch;
}