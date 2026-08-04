using Betalgo.Ranul.OpenAI.ObjectModels;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using System;

namespace Ouroboros.StructuredOutput;

/// <summary>
/// Schema generation and JSON parsing for structured outputs.
/// </summary>
/// <remarks>
/// Internal on purpose: GetSchema returns provider types, and keeping this out of the public
/// surface is what lets us swap the provider SDK without a breaking change.
/// </remarks>
internal static class Json
{
    internal static ResponseFormat GetSchema(Type type)
    {
        if (type is null) throw new ArgumentNullException(nameof(type));

        return new ResponseFormat
        {
            Type = StaticValues.CompletionStatics.ResponseFormat.JsonSchema,
            JsonSchema = new JsonSchema
            {
                Name = type.Name,
                Strict = true,
                Schema = PropertyDefinitionGenerator.GenerateFromType(type)
            }
        };
    }

    /// <summary>
    /// Only returns null if the string is the literal null.
    /// </summary>
    internal static object? ParseJson(string json, Type type)
    {
        return System.Text.Json.JsonSerializer.Deserialize(json, type);
    }
}
