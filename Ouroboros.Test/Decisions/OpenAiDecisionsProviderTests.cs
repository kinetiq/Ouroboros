// The Decisions surface is experimental until OpenAI's API is GA. See DecisionRequestTypeTests.
#pragma warning disable OURO001

using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Ouroboros.Decisions;
using Ouroboros.LargeLanguageModels.Providers;
using Ouroboros.LargeLanguageModels.Providers.OpenAi;
using Ouroboros.Responses;
using Ouroboros.Test.TestSupport;

namespace Ouroboros.Test.Decisions;

/// <summary>
/// The Decisions provider sends the documented request shape, reads every answer type, and labels
/// each failure retryable or final the way the chat providers do.
/// </summary>
/// <remarks>
/// These test one attempt. Whether a retryable attempt is retried is AttemptRunner's job, and its
/// own tests cover that.
/// </remarks>
public class OpenAiDecisionsProviderTests
{
    private const string Answers = """
        {
          "id": "dec_test",
          "model": "gpt-6-luna",
          "answers": [
            { "type": "predicate", "name": "visible_damage", "probability": 0.92 },
            { "type": "choice", "name": "department", "choice": "billing", "confidence": 0.93,
              "probabilities": [ { "value": "billing", "probability": 0.95 }, { "value": "shipping", "probability": 0.05 } ] },
            { "type": "score", "name": "severity", "score": 1.1, "confidence": 0.55,
              "probabilities": [ { "value": 1, "label": "Workaround available", "probability": 0.7 } ] }
          ]
        }
        """;

    private static OuroDecisionRequest Request(params OuroDecisionImage[] images)
    {
        return new OuroDecisionRequest
        {
            Text = "I was charged twice for my order.",
            Images = images,
            Questions =
            [
                new OuroPredicate("visible_damage", "Does the input show visible damage?"),
                new OuroChoice("department",
                [
                    new OuroChoiceOption("billing", "Charges, refunds and invoices"),
                    new OuroChoiceOption("shipping", "Delivery and tracking")
                ]),
                new OuroScore("severity",
                [
                    new OuroScoreLevel("Cosmetic", "No effect on use"),
                    new OuroScoreLevel("Workaround available", "Usable with effort")
                ]) { Instructions = "How badly is the customer affected?" }
            ]
        };
    }

    private static async Task<(ProviderAttempt Attempt, RecordingTransport Transport)> Send(
        HttpStatusCode status, string body, OuroDecisionRequest? request = null)
    {
        var transport = new RecordingTransport((status, body));
        var provider = new OpenAiDecisionsProvider(transport.ToHttpClient(), "test-key");

        var attempt = await provider.SendAsync(request ?? Request(), new DecisionOptions(),
            TestContext.Current.CancellationToken);

        return (attempt, transport);
    }

    [Fact]
    public async Task Posts_To_The_Decisions_Endpoint_With_A_Bearer_Key()
    {
        var (_, transport) = await Send(HttpStatusCode.OK, Answers);

        var sent = Assert.Single(transport.Requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal(OpenAiDecisionsProvider.DefaultEndpoint, sent.Uri);
        Assert.Equal("Bearer test-key", sent.Authorization);
    }

    [Fact]
    public async Task Sends_Each_Question_In_The_Documented_Shape()
    {
        var (_, transport) = await Send(HttpStatusCode.OK, Answers);

        using var body = JsonDocument.Parse(transport.Requests[0].Body);
        var root = body.RootElement;

        Assert.Equal("gpt-6-luna", root.GetProperty("model").GetString());
        Assert.Equal("I was charged twice for my order.", root.GetProperty("input").GetString());

        var questions = root.GetProperty("questions");

        var predicate = questions[0];
        Assert.Equal("predicate", predicate.GetProperty("type").GetString());
        Assert.Equal("visible_damage", predicate.GetProperty("name").GetString());
        Assert.Equal("Does the input show visible damage?", predicate.GetProperty("instructions").GetString());

        var choice = questions[1];
        Assert.Equal("choice", choice.GetProperty("type").GetString());
        Assert.Equal("billing", choice.GetProperty("choices")[0].GetProperty("value").GetString());
        Assert.Equal("Delivery and tracking", choice.GetProperty("choices")[1].GetProperty("description").GetString());

        // Left off when not set, because the guide does not confirm choices accept it.
        Assert.False(choice.TryGetProperty("instructions", out _));

        var score = questions[2];
        Assert.Equal("score", score.GetProperty("type").GetString());
        Assert.Equal("Cosmetic", score.GetProperty("levels")[0].GetProperty("label").GetString());
        Assert.Equal("How badly is the customer affected?", score.GetProperty("instructions").GetString());
    }

    /// <summary>
    /// With an image the input becomes one user message, text first, then each image.
    /// </summary>
    [Fact]
    public async Task Sends_Text_And_Images_As_One_User_Message()
    {
        var request = Request(OuroDecisionImage.FromBytes([1, 2, 3], "image/png"),
            OuroDecisionImage.FromUrl(new Uri("https://example.com/a.png")));

        var (_, transport) = await Send(HttpStatusCode.OK, Answers, request);

        using var body = JsonDocument.Parse(transport.Requests[0].Body);
        var message = Assert.Single(body.RootElement.GetProperty("input").EnumerateArray());

        Assert.Equal("user", message.GetProperty("role").GetString());

        var parts = message.GetProperty("content");
        Assert.Equal("input_text", parts[0].GetProperty("type").GetString());
        Assert.Equal("input_image", parts[1].GetProperty("type").GetString());
        Assert.Equal("data:image/png;base64,AQID", parts[1].GetProperty("image_url").GetString());
        Assert.Equal("https://example.com/a.png", parts[2].GetProperty("image_url").GetString());
    }

    [Fact]
    public async Task Reads_Every_Answer_Type()
    {
        var (attempt, _) = await Send(HttpStatusCode.OK, Answers);

        Assert.False(attempt.IsRetryable);
        var response = Assert.IsType<OuroDecisionResponse>(attempt.Response);

        Assert.Equal("gpt-6-luna", response.Model);
        Assert.Equal(0.92, response.Get<OuroPredicateAnswer>("visible_damage")?.Probability);

        var choice = response.Get<OuroChoiceAnswer>("department");
        Assert.NotNull(choice);
        Assert.Equal("billing", choice.Choice);
        Assert.Equal(0.93, choice.Confidence);
        Assert.Equal(2, choice.Probabilities.Count);

        var score = response.Get<OuroScoreAnswer>("severity");
        Assert.NotNull(score);
        Assert.Equal(1.1, score.Score);
        Assert.Equal("Workaround available", Assert.Single(score.Probabilities).Label);
    }

    /// <summary>
    /// The raw body is the durable record, so it survives even what this version cannot model.
    /// </summary>
    [Fact]
    public async Task Keeps_The_Raw_Body_As_The_Response_Text()
    {
        var (attempt, _) = await Send(HttpStatusCode.OK, Answers);

        Assert.Equal(Answers, attempt.Response.ResponseText);
    }

    [Fact]
    public async Task Reads_A_Refusal_In_Place_Of_An_Answer()
    {
        const string body = """
            { "model": "gpt-6-luna", "answers": [ { "type": "refusal", "name": "identity", "refusal": "No." } ] }
            """;

        var (attempt, _) = await Send(HttpStatusCode.OK, body);

        var response = Assert.IsType<OuroDecisionResponse>(attempt.Response);
        Assert.Equal("No.", response.Get<OuroDecisionRefusal>("identity")?.Reason);
    }

    /// <summary>
    /// A new answer type must not fail a call whose other answers are fine.
    /// </summary>
    [Fact]
    public async Task An_Unknown_Answer_Type_Does_Not_Fail_The_Call()
    {
        const string body = """
            {
              "model": "gpt-6-luna",
              "answers": [
                { "type": "ranking", "name": "order", "ranking": [ "a", "b" ] },
                { "type": "predicate", "name": "angry", "probability": 0.1 }
              ]
            }
            """;

        var (attempt, _) = await Send(HttpStatusCode.OK, body);

        var response = Assert.IsType<OuroDecisionResponse>(attempt.Response);
        Assert.Equal("ranking", response.Get<OuroUnknownAnswer>("order")?.ProviderType);
        Assert.Equal(0.1, response.Get<OuroPredicateAnswer>("angry")?.Probability);
    }

    [Fact]
    public async Task Reads_Usage_When_The_Api_Sends_It()
    {
        const string body = """
            {
              "model": "gpt-6-luna",
              "answers": [ { "type": "predicate", "name": "angry", "probability": 0.1 } ],
              "usage": { "input_tokens": 40, "total_tokens": 40 }
            }
            """;

        var (attempt, _) = await Send(HttpStatusCode.OK, body);

        Assert.Equal(40, attempt.Response.PromptTokens);
        Assert.Equal(40, attempt.Response.TotalTokenUsage);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Rate_Limits_And_Server_Faults_Are_Retryable(HttpStatusCode status)
    {
        var (attempt, _) = await Send(status, """{ "error": { "message": "Slow down.", "type": "rate_limit", "code": null } }""");

        Assert.True(attempt.IsRetryable);
        Assert.IsType<OuroResponseProviderError>(attempt.Response);
    }

    /// <summary>
    /// A request the API rejected fails the same way every time. Retrying it only spends money.
    /// </summary>
    [Fact]
    public async Task A_Rejected_Request_Is_Final_And_Carries_The_Error()
    {
        const string body = """
            { "error": { "message": "Unknown question type.", "type": "invalid_request_error", "code": "invalid_question" } }
            """;

        var (attempt, _) = await Send(HttpStatusCode.BadRequest, body);

        Assert.False(attempt.IsRetryable);
        var error = Assert.IsType<OuroResponseProviderError>(attempt.Response);
        Assert.Equal("OpenAI", error.ErrorOrigin);
        Assert.Equal("invalid_question", error.ErrorCode);
        Assert.Equal("Unknown question type.", error.ErrorDetails);
    }

    /// <summary>
    /// Proxies and gateways answer with HTML or nothing. The status still has to reach the caller.
    /// </summary>
    [Fact]
    public async Task An_Error_Without_A_Json_Body_Still_Reports_The_Status()
    {
        var (attempt, _) = await Send(HttpStatusCode.BadGateway, "<html>Bad gateway</html>");

        Assert.True(attempt.IsRetryable);
        var error = Assert.IsType<OuroResponseProviderError>(attempt.Response);
        Assert.Equal("502", error.ErrorCode);
    }

    /// <summary>
    /// A missing probability is a broken answer. Reading it as 0 would be a confident "no".
    /// </summary>
    [Fact]
    public async Task A_Malformed_Answer_Fails_Instead_Of_Reading_As_Zero()
    {
        const string body = """
            { "model": "gpt-6-luna", "answers": [ { "type": "predicate", "name": "angry" } ] }
            """;

        var (attempt, _) = await Send(HttpStatusCode.OK, body);

        Assert.False(attempt.IsRetryable);
        var error = Assert.IsType<OuroResponseInternalError>(attempt.Response);
        Assert.Contains("probability", error.ResponseText);
    }

    [Fact]
    public async Task A_Body_That_Is_Not_Json_Fails_As_Final()
    {
        var (attempt, _) = await Send(HttpStatusCode.OK, "not json");

        Assert.False(attempt.IsRetryable);
        Assert.IsType<OuroResponseInternalError>(attempt.Response);
    }
}
