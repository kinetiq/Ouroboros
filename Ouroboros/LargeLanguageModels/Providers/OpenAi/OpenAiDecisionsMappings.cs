using System;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ouroboros.Decisions;

namespace Ouroboros.LargeLanguageModels.Providers.OpenAi;

/// <summary>
/// Translates between the Decisions types and the JSON of OpenAI's /v1/decisions endpoint.
/// </summary>
/// <remarks>
/// Hand-written because the OpenAI .NET SDK has no Decisions client yet. Replace this with the SDK's
/// types when one ships; nothing outside this file and the provider sees the wire shape.
///
/// The shapes follow OpenAI's Decisions guide. A few details the guide does not show are marked
/// UNCONFIRMED where they are used, and the live test is what settles them. The API is in beta, so
/// this file is where a change to it lands.
/// </remarks>
[Experimental(DecisionsDiagnostics.ExperimentalId)]
internal static class OpenAiDecisionsMappings
{
    /// <summary>
    /// Builds the request body.
    /// </summary>
    public static string MapRequest(OuroDecisionRequest request, DecisionOptions options)
    {
        var body = new JsonObject
        {
            ["model"] = options.Model,
            ["input"] = MapInput(request),
            ["questions"] = new JsonArray(request.Questions.Select(MapQuestion).ToArray<JsonNode?>())
        };

        return body.ToJsonString();
    }

    /// <summary>
    /// Text alone goes as a plain string, which the guide shows. Anything with an image goes as one
    /// user message holding text and image parts.
    /// </summary>
    private static JsonNode MapInput(OuroDecisionRequest request)
    {
        if (request.Images.Count == 0)
            return JsonValue.Create(request.Text ?? "");

        var parts = new JsonArray();

        // The guide shows the input_image part but not a text part. input_text, the Responses API's
        // name, is accepted: confirmed by DecisionsLiveTests.Text_And_An_Image_Round_Trip.
        if (!string.IsNullOrEmpty(request.Text))
            parts.Add(new JsonObject { ["type"] = "input_text", ["text"] = request.Text });

        foreach (var image in request.Images)
            parts.Add(new JsonObject { ["type"] = "input_image", ["image_url"] = image.Url });

        return new JsonArray(new JsonObject { ["role"] = "user", ["content"] = parts });
    }

    private static JsonObject MapQuestion(OuroDecisionQuestion question)
    {
        var mapped = new JsonObject { ["name"] = question.Name };

        switch (question)
        {
            case OuroPredicate:
                mapped["type"] = "predicate";
                break;

            case OuroChoice choice:
                mapped["type"] = "choice";
                mapped["choices"] = new JsonArray(choice.Options
                    .Select(option => new JsonObject
                    {
                        ["value"] = option.Value,
                        ["description"] = option.Description
                    })
                    .ToArray<JsonNode?>());
                break;

            case OuroScore score:
                mapped["type"] = "score";
                mapped["levels"] = new JsonArray(score.Levels
                    .Select(level => new JsonObject
                    {
                        ["label"] = level.Label,
                        ["description"] = level.Description
                    })
                    .ToArray<JsonNode?>());
                break;

            // The hierarchy is closed, so this only fires if a question type is added here without
            // a mapping. Failing beats sending a question the API would answer as something else.
            default:
                throw new InvalidOperationException(
                    $"No mapping for question type {question.GetType().Name}. This should never happen.");
        }

        // The guide names instructions only on predicates, but choices and scores accept them too:
        // confirmed by DecisionsLiveTests.Every_Question_Type_Round_Trips. Sent only when set.
        if (question.Instructions is { } instructions)
            mapped["instructions"] = instructions;

        return mapped;
    }

    /// <summary>
    /// Parses a 2xx body into a response.
    /// </summary>
    /// <exception cref="JsonException">The body is not the shape this expects.</exception>
    public static OuroDecisionResponse MapResponse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("answers", out var answersElement) || answersElement.ValueKind != JsonValueKind.Array)
            throw new JsonException("The response has no answers array.");

        var answers = answersElement.EnumerateArray().Select(MapAnswer).ToList();

        var response = new OuroDecisionResponse(answers, json)
        {
            Model = GetString(root, "model") ?? ""
        };

        // The guide does not show usage, but the API sends it in the Responses API's shape, with
        // output_tokens always 0 (output is not billed). Confirmed by the live tests. Still read
        // defensively, so a response without it leaves the counts at 0 rather than failing.
        if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
        {
            response.PromptTokens = GetInt(usage, "input_tokens") ?? 0;
            response.CompletionTokens = GetInt(usage, "output_tokens");
            response.TotalTokenUsage = GetInt(usage, "total_tokens") ?? response.PromptTokens;
        }

        return response;
    }

    /// <summary>
    /// Maps one answer. A type this version does not model becomes OuroUnknownAnswer, not an error,
    /// so a new answer type from OpenAI does not fail a call whose other answers are fine.
    /// </summary>
    private static OuroDecisionAnswer MapAnswer(JsonElement answer)
    {
        var name = GetString(answer, "name") ?? "";
        var type = GetString(answer, "type") ?? "";

        return type switch
        {
            "predicate" => new OuroPredicateAnswer(name, GetRequiredDouble(answer, "probability")),

            "choice" => new OuroChoiceAnswer(name,
                GetString(answer, "choice") ?? throw new JsonException($"Choice answer '{name}' has no choice."),
                GetRequiredDouble(answer, "confidence"),
                MapArray(answer, "probabilities", item => new OuroChoiceProbability(
                    GetString(item, "value") ?? "",
                    GetRequiredDouble(item, "probability")))),

            "score" => new OuroScoreAnswer(name,
                GetRequiredDouble(answer, "score"),
                GetRequiredDouble(answer, "confidence"),
                MapArray(answer, "probabilities", item => new OuroScoreProbability(
                    GetInt(item, "value") ?? 0,
                    GetString(item, "label") ?? "",
                    GetRequiredDouble(item, "probability")))),

            // UNCONFIRMED: the guide says an answer may be a refusal but does not show one. A
            // "refusal" string is how OpenAI words it on Chat Completions; "reason" is the fallback.
            "refusal" => new OuroDecisionRefusal(name, GetString(answer, "refusal") ?? GetString(answer, "reason")),

            _ => new OuroUnknownAnswer(name, type)
        };
    }

    /// <summary>
    /// Reads an OpenAI error body: { "error": { "message", "type", "code" } }.
    /// </summary>
    /// <returns>Nulls when the body is not that shape, which a proxy or gateway error often is.</returns>
    public static (string? Code, string? Message) MapError(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object)
                return (null, null);

            // code is often null on OpenAI errors, with the useful word in type instead.
            return (GetString(error, "code") ?? GetString(error, "type"), GetString(error, "message"));
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static IReadOnlyList<T> MapArray<T>(JsonElement parent, string property, Func<JsonElement, T> map)
    {
        if (!parent.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
            return [];

        return array.EnumerateArray().Select(map).ToList();
    }

    private static string? GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? GetInt(JsonElement element, string property)
    {
        // TryGetInt32, not GetInt32: a 1.0 or an out-of-range number throws FormatException from the
        // latter, which would escape the provider's JsonException catch as an unexplained fault.
        return element.TryGetProperty(property, out var value)
               && value.ValueKind == JsonValueKind.Number
               && value.TryGetInt32(out var number)
            ? number
            : null;
    }

    /// <summary>
    /// A missing probability is a malformed answer, not a zero. Reading it as 0 would turn a broken
    /// response into a confident "no".
    /// </summary>
    private static double GetRequiredDouble(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number)
            return value.GetDouble();

        throw new JsonException($"An answer is missing its {property}.");
    }
}
