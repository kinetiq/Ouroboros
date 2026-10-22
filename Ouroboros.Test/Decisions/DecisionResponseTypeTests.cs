// The Decisions surface is experimental until OpenAI's API is GA. See DecisionRequestTypeTests.
#pragma warning disable OURO001

using Ouroboros.Decisions;
using Ouroboros.Responses;

namespace Ouroboros.Test.Decisions;

/// <summary>
/// The response hands back each answer by name, and a refusal never passes for an answer.
/// </summary>
public class DecisionResponseTypeTests
{
    private static OuroDecisionResponse Response()
    {
        return new OuroDecisionResponse(
        [
            new OuroPredicateAnswer("visible_damage", 0.92),
            new OuroChoiceAnswer("department", "billing", 0.93,
                [new OuroChoiceProbability("billing", 0.95), new OuroChoiceProbability("shipping", 0.05)]),
            new OuroScoreAnswer("severity", 1.1, 0.55,
                [new OuroScoreProbability(1, "Workaround available", 0.7)]),
            new OuroDecisionRefusal("identity", "Declined to identify a person.")
        ], """{ "raw": true }""");
    }

    [Fact]
    public void Get_Returns_The_Named_Answer_As_Its_Type()
    {
        var response = Response();

        Assert.Equal(0.92, response.Get<OuroPredicateAnswer>("visible_damage")?.Probability);
        Assert.Equal("billing", response.Get<OuroChoiceAnswer>("department")?.Choice);
        Assert.Equal(1.1, response.Get<OuroScoreAnswer>("severity")?.Score);
    }

    [Fact]
    public void Get_Returns_Null_For_A_Name_Not_In_The_Response()
    {
        Assert.Null(Response().Get<OuroPredicateAnswer>("not_asked"));
    }

    /// <summary>
    /// A refusal stands in for the answer. Asked for as the answer type, it must not pass for one.
    /// </summary>
    [Fact]
    public void A_Refusal_Is_Not_Returned_As_An_Answer()
    {
        var response = Response();

        Assert.Null(response.Get<OuroChoiceAnswer>("identity"));
        Assert.Equal("Declined to identify a person.", response.Get<OuroDecisionRefusal>("identity")?.Reason);
        Assert.True(response.HasRefusals);
    }

    [Fact]
    public void Get_Returns_Null_When_The_Type_Does_Not_Match()
    {
        Assert.Null(Response().Get<OuroScoreAnswer>("department"));
    }

    /// <summary>
    /// Failures share ChatAsync's types, so a success has to say so the same way.
    /// </summary>
    [Fact]
    public void Is_A_Success_That_Keeps_The_Raw_Body()
    {
        OuroResponseBase response = Response();

        Assert.True(response.Success);
        Assert.Equal("""{ "raw": true }""", response.ResponseText);
    }

    [Fact]
    public void Answers_Keep_The_Order_The_Api_Returned()
    {
        var names = Response().Answers.Select(answer => answer.Name);

        Assert.Equal(["visible_damage", "department", "severity", "identity"], names);
    }

    [Fact]
    public void Each_Answer_Reports_Its_Kind()
    {
        var kinds = Response().Answers.Select(answer => answer.Kind);

        Assert.Equal(
            [
                OuroDecisionAnswerKind.Predicate, OuroDecisionAnswerKind.Choice,
                OuroDecisionAnswerKind.Score, OuroDecisionAnswerKind.Refusal
            ],
            kinds);

        Assert.Equal(OuroDecisionAnswerKind.Unknown, new OuroUnknownAnswer("x", "ranking").Kind);
    }

    /// <summary>
    /// Anything persisting the numeric kind depends on these values. Append; never renumber.
    /// </summary>
    [Fact]
    public void Kind_Values_Are_Stable()
    {
        Assert.Equal(0, (int)OuroDecisionAnswerKind.Unknown);
        Assert.Equal(1, (int)OuroDecisionAnswerKind.Predicate);
        Assert.Equal(2, (int)OuroDecisionAnswerKind.Choice);
        Assert.Equal(3, (int)OuroDecisionAnswerKind.Score);
        Assert.Equal(4, (int)OuroDecisionAnswerKind.Refusal);
    }
}
