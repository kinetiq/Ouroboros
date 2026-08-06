using System;
using System.Text.Json;

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
    /// Default options, deliberately: matching is case-sensitive, which is what makes the schema
    /// generator's exact-PascalCase rule load-bearing. Relaxing this here would mask a schema that
    /// emits the wrong casing rather than fixing it.
    /// </summary>
    private static object? Deserialize(string json, Type type)
    {
        return JsonSerializer.Deserialize(json, type);
    }
}
