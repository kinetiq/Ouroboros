using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ouroboros.StructuredOutput;

/// <summary>
/// Turns a structured-output response back into the caller's type.
/// </summary>
/// <remarks>
/// Shared by every provider so the contract is identical whichever one served the call: on success
/// the caller gets their type, and on a parse failure they get <c>null</c> - never an exception.
/// Null is a first-class signal consumers branch on (retry the turn, fail the step, skip the item),
/// and turning it into a throw would break all of them at once.
/// </remarks>
internal static class ResponseParser
{
    /// <summary>
    /// Deserializes the response text into <paramref name="responseType" />, or null if it will not
    /// parse. Returns null immediately when no type was requested.
    /// </summary>
    public static object? Parse(Type? responseType, string responseText)
    {
        if (responseType is null)
            return null;

        try
        {
            return Deserialize(responseText, responseType);
        }
        catch (JsonException)
        {
            // Reasoning models sometimes narrate before the JSON. The payload is then the last
            // object in the text, so try that before giving up.
            var lastIndex = responseText.LastIndexOf("\n{", StringComparison.Ordinal);

            if (lastIndex >= 0)
            {
                try
                {
                    return Deserialize(responseText[(lastIndex + 1)..], responseType);
                }
                catch (JsonException)
                {
                    // Fall through - a null result is the documented signal.
                }
            }

            return null;
        }
    }

    /// <summary>
    /// The reading half of the contract the schema generator writes.
    /// </summary>
    /// <remarks>
    /// Property matching stays case-sensitive - the default - which is what makes the generator's
    /// exact-PascalCase rule load-bearing. Turning on case-insensitive matching here would mask a
    /// schema emitting the wrong casing rather than fixing it.
    ///
    /// The one addition is the string-enum converter, and it is not a relaxation: JsonSchemaGenerator
    /// emits enums as a string with an <c>enum</c> list of the member names, so the model returns
    /// "Pass" rather than 0. Without this converter System.Text.Json only accepts the number, the
    /// parse throws, and Parse returns null - a successful response with an empty ResponseObject and
    /// nothing anywhere saying why. The two halves have to speak the same dialect.
    /// </remarks>
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static object? Deserialize(string json, Type type)
    {
        return JsonSerializer.Deserialize(json, type, Options);
    }
}
