using System.Collections.Generic;
using System.Linq;
using Ouroboros.Core;
using Ouroboros.Responses;

namespace Ouroboros.Test.Responses;

/// <summary>
/// Covers the content-block model and, above all, the invariant that keeps it additive:
/// ResponseText still reads exactly as it did before blocks existed.
/// </summary>
public class ContentBlockTests
{
    /// <summary>
    /// The compatibility guarantee. Every existing consumer reads ResponseText, and a plain chat
    /// turn is one text block - so for that case the two constructors must agree exactly.
    /// </summary>
    [Fact]
    public void A_Single_Text_Block_Produces_The_Same_ResponseText_As_Before()
    {
        const string text = "The quick brown fox.";

        var fromBlocks = new OuroResponseSuccess([new OuroTextBlock(text)]);
        var fromString = new OuroResponseSuccess(text);

        Assert.Equal(fromString.ResponseText, fromBlocks.ResponseText);
        Assert.Equal(text, fromBlocks.ResponseText);
    }

    /// <summary>
    /// The reason stdout lives in a block rather than in the text: a caller who ignores blocks gets
    /// the model's prose and nothing else, exactly as it did before code execution existed.
    /// </summary>
    [Fact]
    public void Code_Execution_Output_Does_Not_Leak_Into_ResponseText()
    {
        var response = new OuroResponseSuccess([
            new OuroTextBlock("Here is the mean."),
            new OuroCodeExecutionBlock
            {
                Code = "print(df.mean())",
                Result = new OuroCodeExecutionResult { Stdout = "42.0", ExitCode = 0 }
            }
        ]);

        Assert.Equal("Here is the mean.", response.ResponseText);
        Assert.DoesNotContain("42.0", response.ResponseText);
        Assert.Equal("42.0", Assert.Single(response.CodeExecutions).Result!.Stdout);
    }

    [Fact]
    public void Multiple_Text_Blocks_Join_With_A_Blank_Line()
    {
        var response = new OuroResponseSuccess([
            new OuroTextBlock("First."),
            new OuroCodeExecutionBlock { Code = "x = 1" },
            new OuroTextBlock("Second.")
        ]);

        Assert.Equal("First.\n\nSecond.", response.ResponseText);
    }

    /// <summary>
    /// Hand-built responses are what tests stub, so Content has to be enumerable without a null
    /// check even though nothing set it.
    /// </summary>
    [Fact]
    public void A_Hand_Built_Response_Has_Empty_Content_Not_Null()
    {
        var response = new OuroResponseSuccess("stubbed");

        Assert.NotNull(response.Content);
        Assert.Empty(response.Content);
        Assert.Empty(response.CodeExecutions);
        Assert.Equal("stubbed", response.ResponseText);
    }

    /// <summary>
    /// New block types reach code compiled against an older version, so an unrecognised block has
    /// to be an ordinary value rather than an exception.
    /// </summary>
    [Fact]
    public void An_Unknown_Block_Survives_And_Contributes_No_Text()
    {
        var response = new OuroResponseSuccess([
            new OuroTextBlock("Visible."),
            new OuroUnknownBlock("some_future_block_type")
        ]);

        Assert.Equal("Visible.", response.ResponseText);

        var unknown = Assert.IsType<OuroUnknownBlock>(response.Content.Last());
        Assert.Equal(OuroBlockKind.Unknown, unknown.Kind);
        Assert.Equal("some_future_block_type", unknown.ProviderType);
    }

    [Theory]
    [InlineData(OuroStopReason.EndTurn, true)]
    [InlineData(OuroStopReason.StopSequence, true)]
    [InlineData(OuroStopReason.Unknown, true)]
    [InlineData(OuroStopReason.MaxTokens, false)]
    [InlineData(OuroStopReason.Paused, false)]
    public void IsComplete_Is_False_Only_When_Truncation_Was_Positively_Reported(
        OuroStopReason stopReason, bool expected)
    {
        var response = new OuroResponseSuccess("text") { StopReason = stopReason };

        Assert.Equal(expected, response.IsComplete);
    }

    /// <summary>
    /// Blocks are records, so value equality comes for free - worth pinning, since consumers will
    /// reasonably compare them.
    /// </summary>
    [Fact]
    public void Blocks_Compare_By_Value()
    {
        Assert.Equal(new OuroTextBlock("same"), new OuroTextBlock("same"));
        Assert.NotEqual(new OuroTextBlock("a"), new OuroTextBlock("b"));
    }
}
