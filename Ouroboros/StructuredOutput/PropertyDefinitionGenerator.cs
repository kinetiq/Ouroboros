using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Serialization;
using Betalgo.Ranul.OpenAI.ObjectModels.SharedModels;

namespace Ouroboros.StructuredOutput;

/// <summary>
/// Borrowed from Betalgo's utility lib, which currently states that it is broken. 
/// </summary>
internal class PropertyDefinitionGenerator
{
    internal static PropertyDefinition GenerateFromType(Type type)
    {
        // Check for Description attribute on the type
        var description = GetDescription(type);

        if (type == null)
            throw new ArgumentNullException(nameof(type));

        if (type.IsPrimitive || type == typeof(string) || type == typeof(DateTime))
        {
            return GeneratePrimitiveDefinition(type, description);
        }
        else if (type.IsEnum)
        {
            return GenerateEnumDefinition(type, description);
        }
        else if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)))
        {
            return GenerateArrayDefinition(type, description);
        }
        else
        {
            return GenerateObjectDefinition(type, description);
        }
    }

    internal static PropertyDefinition GenerateFromPropertyInfo(PropertyInfo info)
    {
        // Check for Description attribute on the type
        var description = GetDescription(info);
        var type = info.PropertyType;

        if (info == null)
            throw new ArgumentNullException(nameof(info));

        if (type.IsPrimitive || type == typeof(string) || type == typeof(DateTime))
        {
            return GeneratePrimitiveDefinition(type, description);
        }
        else if (type.IsEnum)
        {
            return GenerateEnumDefinition(type, description);
        }
        else if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)))
        {
            return GenerateArrayDefinition(type, description);
        }
        else
        {
            return GenerateObjectDefinition(type, description);
        }
    }


    private static string? GetDescription(Type type)
    {
        string? description = null;
        var descriptionAttr = type.GetCustomAttribute<DescriptionAttribute>();
        if (descriptionAttr != null)
        {
            description = descriptionAttr.Description;
        }

        return description;
    }

    private static string? GetDescription(PropertyInfo property)
    {
        return property.GetCustomAttribute<DescriptionAttribute>()?
            .Description;
    }

    private static PropertyDefinition GeneratePrimitiveDefinition(Type type, string? description)
    {
        if (type == typeof(string))
            return PropertyDefinition.DefineString(description);
        else if (type == typeof(int) || type == typeof(long))
            return PropertyDefinition.DefineInteger(description);
        else if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            return PropertyDefinition.DefineNumber(description);
        else if (type == typeof(bool))
            return PropertyDefinition.DefineBoolean(description);
        else if (type == typeof(DateTime))
            return PropertyDefinition.DefineString(description ?? "ISO 8601 date-time string");
        else
            throw new ArgumentException($"Unsupported primitive type: {type.Name}");
    }

    private static PropertyDefinition GenerateEnumDefinition(Type type, string? description)
    {
        var enumValues = Enum.GetNames(type);
        return PropertyDefinition.DefineEnum([.. enumValues], description ?? $"Enum of type {type.Name}");
    }

    private static PropertyDefinition GenerateArrayDefinition(Type type, string? description)
    {
        Type elementType = type.IsArray ?
            type.GetElementType()! :
            type.GetGenericArguments()[0];

        var items = GenerateFromType(elementType);

        // Note that this doesn't take description - not supported by OpenAI for some reason.

        return PropertyDefinition.DefineArray(items);
    }

    private static PropertyDefinition GenerateObjectDefinition(Type type, string? description)
    {
        var properties = new Dictionary<string, PropertyDefinition>();
        var required = new List<string>();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            string propertyName = GetJsonPropertyName(prop);
            properties[propertyName] = GenerateFromPropertyInfo(prop);
            required.Add(propertyName);
        }

        return PropertyDefinition.DefineObject(
            properties,
            required,
            false, // Set additionalProperties to false by default
            description ?? $"Object of type {type.Name}",
            null
        );
    }

    private static string GetJsonPropertyName(PropertyInfo prop)
    {
        var jsonPropertyNameAttribute = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
        return jsonPropertyNameAttribute != null ? jsonPropertyNameAttribute.Name : prop.Name;
    }
}