// The Decisions surface is experimental until OpenAI's API is GA. Suppressed per file rather than in
// the csproj, so the rest of the suite still fails to build if it touches it by accident.
#pragma warning disable OURO001

using System;
using Ouroboros.Decisions;

namespace Ouroboros.Test.Decisions;

/// <summary>
/// The request types refuse what can never work, at construction, before a call is made.
/// </summary>
/// <remarks>
/// Only mistakes the type alone can see are caught here. Rules that need the whole request, such as
/// unique question names, are checked when the request is sent.
/// </remarks>
public class DecisionRequestTypeTests
{
    [Fact]
    public void FromBytes_Builds_A_Base64_Data_Url()
    {
        var image = OuroDecisionImage.FromBytes([1, 2, 3], "image/png");

        Assert.Equal("data:image/png;base64,AQID", image.Url);
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    public void FromBytes_Refuses_A_Media_Type_That_Is_Not_An_Image(string mediaType)
    {
        var ex = Assert.Throws<ArgumentException>(() => OuroDecisionImage.FromBytes([1], mediaType));

        Assert.Contains(mediaType, ex.Message);
    }

    [Fact]
    public void FromBytes_Refuses_An_Empty_Image()
    {
        Assert.Throws<ArgumentException>(() => OuroDecisionImage.FromBytes([], "image/png"));
    }

    [Theory]
    [InlineData("https://example.com/a.png")]
    [InlineData("http://example.com/a.png")]
    public void FromUrl_Accepts_A_Web_Url(string url)
    {
        var image = OuroDecisionImage.FromUrl(new Uri(url));

        Assert.Equal(url, image.Url);
    }

    /// <summary>
    /// OpenAI fetches the image itself, so anything it cannot reach is refused here, not as a 400.
    /// </summary>
    [Theory]
    [InlineData("file:///c:/images/a.png")]
    [InlineData("ftp://example.com/a.png")]
    [InlineData("data:image/png;base64,AQID")]
    public void FromUrl_Refuses_A_Url_OpenAI_Cannot_Fetch(string url)
    {
        Assert.Throws<ArgumentException>(() => OuroDecisionImage.FromUrl(new Uri(url)));
    }

    [Fact]
    public void FromUrl_Refuses_A_Relative_Url()
    {
        Assert.Throws<ArgumentException>(() =>
            OuroDecisionImage.FromUrl(new Uri("images/a.png", UriKind.Relative)));
    }

    /// <summary>
    /// A data URL holds the whole image. Printing it would flood a log with base64.
    /// </summary>
    [Fact]
    public void ToString_Does_Not_Print_An_Inline_Image()
    {
        var image = OuroDecisionImage.FromBytes(new byte[3000], "image/png");

        var text = image.ToString();

        Assert.DoesNotContain("base64", text);
        Assert.True(text.Length < 100);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_Question_Needs_A_Name(string name)
    {
        Assert.Throws<ArgumentException>(() => new OuroPredicate(name, "Is the customer angry?"));
        Assert.Throws<ArgumentException>(() => new OuroChoice(name, [new("a", "A")]));
        Assert.Throws<ArgumentException>(() => new OuroScore(name, [new("low", "Low")]));
    }

    /// <summary>
    /// For a predicate the instructions are the condition. Without them there is nothing to ask.
    /// </summary>
    [Fact]
    public void A_Predicate_Needs_Instructions()
    {
        Assert.Throws<ArgumentException>(() => new OuroPredicate("angry", " "));
    }

    [Fact]
    public void A_Predicate_Keeps_Its_Instructions()
    {
        var predicate = new OuroPredicate("angry", "Is the customer angry?");

        Assert.Equal("angry", predicate.Name);
        Assert.Equal("Is the customer angry?", predicate.Instructions);
    }

    [Fact]
    public void Defaults_Suit_A_Fast_Call()
    {
        var options = new DecisionOptions();

        Assert.Equal("gpt-6-luna", options.Model);
        Assert.True(options.UseExponentialBackOff);
        Assert.Null(options.Timeout);
        Assert.True(DecisionOptions.DefaultTimeout < Constants.DefaultAttemptTimeout);
    }
}
