using Betalgo.Ranul.OpenAI.ObjectModels;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using System;


namespace Ouroboros.StructuredOutput;
public class Json
{
    public static ResponseFormat GetSchema(Type type)
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

    // existing generic helper stays for convenience
    public static ResponseFormat GetSchema<T>() where T : class => GetSchema(typeof(T));

    /// <summary>
    /// Returns Only returns null if the string is the literal null.
    /// </summary>
    public static T? ParseJson<T>(string json) where T : class
    {
        return System.Text.Json.JsonSerializer.Deserialize<T>(json);
    }

    /// <summary>
    /// Returns Only returns null if the string is the literal null.
    /// </summary>
    public static object? ParseJson(string json, Type type)
    {
        return System.Text.Json.JsonSerializer.Deserialize(json, type);
    }
}