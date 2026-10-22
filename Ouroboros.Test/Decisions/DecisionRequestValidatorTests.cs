// The Decisions surface is experimental until OpenAI's API is GA. See DecisionRequestTypeTests.
#pragma warning disable OURO001

using System;
using System.Net;
using System.Threading.Tasks;
using Ouroboros.Decisions;
using Ouroboros.LargeLanguageModels.Providers.OpenAi;
using Ouroboros.Responses;
using Ouroboros.Test.TestSupport;

namespace Ouroboros.Test.Decisions;

/// <summary>
/// A request that cannot work is refused before a call is spent on it, with every problem named.
/// </summary>
public class DecisionRequestValidatorTests
{
    private static OuroChoice Department() => new("department",
    [
        new OuroChoiceOption("billing", "Charges and refunds"),
        new OuroChoiceOption("shipping", "Delivery and tracking")
    ]);

    private static OuroDecisionRequest Valid() => new()
    {
        Text = "I was charged twice.",
        Questions = [new OuroPredicate("angry", "Is the customer angry?"), Department()]
    };

    private static string? Validate(OuroDecisionRequest request, DecisionOptions? options = null)
    {
        return DecisionRequestValidator.Validate(request, options ?? new DecisionOptions());
    }

    [Fact]
    public void A_Valid_Request_Passes()
    {
        Assert.Null(Validate(Valid()));
    }

    [Fact]
    public void Images_Alone_Are_Enough_Evidence()
    {
        var request = Valid() with { Text = null, Images = [OuroDecisionImage.FromBytes([1], "image/png")] };

        Assert.Null(Validate(request));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_Request_With_Nothing_To_Evaluate_Is_Refused(string? text)
    {
        var problems = Validate(Valid() with { Text = text });

        Assert.NotNull(problems);
        Assert.Contains("nothing to evaluate", problems);
    }

    [Fact]
    public void A_Request_With_No_Questions_Is_Refused()
    {
        Assert.Contains("no questions", Validate(Valid() with { Questions = [] }));
        Assert.Contains("no questions", Validate(Valid() with { Questions = null! }));
    }

    [Fact]
    public void Null_Entries_Are_Refused()
    {
        Assert.Contains("Questions contains a null", Validate(Valid() with { Questions = [null!] }));
        Assert.Contains("Images contains a null", Validate(Valid() with { Images = [null!] }));
        Assert.Contains("Images is null", Validate(Valid() with { Images = null! }));
    }

    /// <summary>
    /// Answers come back by name. Two questions with one name would leave one answer unreachable.
    /// </summary>
    [Fact]
    public void Duplicate_Question_Names_Are_Refused()
    {
        var request = Valid() with
        {
            Questions = [new OuroPredicate("angry", "Angry?"), new OuroPredicate("angry", "Very angry?")]
        };

        Assert.Contains("'angry'", Validate(request));
    }

    [Fact]
    public void Names_That_Differ_Only_By_Case_Are_Allowed()
    {
        var request = Valid() with
        {
            Questions = [new OuroPredicate("angry", "Angry?"), new OuroPredicate("Angry", "Very angry?")]
        };

        Assert.Null(Validate(request));
    }

    /// <summary>
    /// The constructor requires instructions, but a with expression can still clear them.
    /// </summary>
    [Fact]
    public void A_Predicate_Whose_Instructions_Were_Cleared_Is_Refused()
    {
        var cleared = new OuroPredicate("angry", "Angry?") with { Instructions = null };

        Assert.Contains("Predicate 'angry' has no instructions", Validate(Valid() with { Questions = [cleared] }));
    }

    [Fact]
    public void A_Choice_Needs_Two_Options()
    {
        var single = new OuroChoice("department", [new OuroChoiceOption("billing", "Charges")]);

        Assert.Contains("at least two", Validate(Valid() with { Questions = [single] }));
    }

    [Fact]
    public void A_Choice_Option_Needs_A_Value_And_A_Description()
    {
        var blank = new OuroChoice("department",
        [
            new OuroChoiceOption("", "Charges"),
            new OuroChoiceOption("shipping", " ")
        ]);

        var problems = Validate(Valid() with { Questions = [blank] });

        Assert.Contains("no value", problems);
        Assert.Contains("no description", problems);
    }

    [Fact]
    public void Duplicate_Choice_Values_Are_Refused()
    {
        var duplicate = new OuroChoice("department",
        [
            new OuroChoiceOption("billing", "Charges"),
            new OuroChoiceOption("billing", "Refunds")
        ]);

        Assert.Contains("value 'billing'", Validate(Valid() with { Questions = [duplicate] }));
    }

    [Fact]
    public void A_Score_Needs_Two_Levels_With_Labels_And_Descriptions()
    {
        var single = new OuroScore("severity", [new OuroScoreLevel("Low", "Minor")]);
        Assert.Contains("at least two", Validate(Valid() with { Questions = [single] }));

        var blank = new OuroScore("severity", [new OuroScoreLevel("", "Minor"), new OuroScoreLevel("High", "")]);
        var problems = Validate(Valid() with { Questions = [blank] });

        Assert.Contains("no label", problems);
        Assert.Contains("no description", problems);
    }

    [Fact]
    public void Duplicate_Score_Labels_Are_Refused()
    {
        var duplicate = new OuroScore("severity", [new OuroScoreLevel("Low", "Minor"), new OuroScoreLevel("Low", "Small")]);

        Assert.Contains("labelled 'Low'", Validate(Valid() with { Questions = [duplicate] }));
    }

    [Fact]
    public void Bad_Options_Are_Refused()
    {
        Assert.Contains("Model is empty", Validate(Valid(), new DecisionOptions { Model = " " }));
        Assert.Contains("Timeout", Validate(Valid(), new DecisionOptions { Timeout = TimeSpan.Zero }));
    }

    /// <summary>
    /// Three mistakes should take one run to find, not three.
    /// </summary>
    [Fact]
    public void Every_Problem_Is_Reported_At_Once()
    {
        var request = new OuroDecisionRequest
        {
            Questions =
            [
                new OuroPredicate("angry", "Angry?"),
                new OuroPredicate("angry", "Very angry?"),
                new OuroChoice("department", [new OuroChoiceOption("billing", "Charges")])
            ]
        };

        var problems = Validate(request);

        Assert.NotNull(problems);
        Assert.Contains("nothing to evaluate", problems);
        Assert.Contains("'angry'", problems);
        Assert.Contains("at least two", problems);
    }

    /// <summary>
    /// The point of validating: the provider refuses without spending a call, and does not retry.
    /// </summary>
    [Fact]
    public async Task The_Provider_Refuses_An_Invalid_Request_Without_Calling_The_Api()
    {
        var transport = new RecordingTransport((HttpStatusCode.OK, "{}"));
        var provider = new OpenAiDecisionsProvider(transport.ToHttpClient(), "test-key");

        var attempt = await provider.SendAsync(Valid() with { Questions = [] }, new DecisionOptions(),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, transport.Calls);
        Assert.False(attempt.IsRetryable);
        Assert.IsType<OuroResponseInternalError>(attempt.Response);
    }
}
