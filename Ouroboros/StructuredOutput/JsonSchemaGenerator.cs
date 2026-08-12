using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Ouroboros.StructuredOutput;

/// <summary>
/// Builds a JSON Schema from a CLR type, in the strict-mode dialect both providers accept.
/// </summary>
/// <remarks>
/// Provider-neutral by design. It replaced a generator that emitted Betalgo's PropertyDefinition
/// objects, which tied structured output to one SDK and was the last thing blocking a second
/// provider from supporting it at all.
///
/// Two rules matter more than they look:
///
/// Property names are emitted <b>exactly as declared</b> - PascalCase, no naming policy. The
/// response is deserialized with default System.Text.Json options, which are case-sensitive, so a
/// camelCased schema would produce a perfectly successful parse into an object with every property
/// left at its default. Non-null, no exception, all fields empty.
///
/// Anything not understood <b>throws</b> rather than being approximated. A schema that is quietly
/// wrong produces a model response that is quietly wrong, and the first person to notice is
/// whoever reads the output weeks later.
/// </remarks>
/// <remarks>
/// Public so callers can see what their ResponseType actually produces. Two uses beyond curiosity:
/// checking at build time that a type is usable at all - the unsupported shapes below throw, and
/// without this that only surfaces on a live call - and reading the schema back when a model returns
/// nulls, which is otherwise guesswork.
/// </remarks>
public static class JsonSchemaGenerator
{
    /// <summary>
    /// How deep nesting may go before we assume a cycle.
    /// </summary>
    /// <remarks>
    /// The previous generator had no guard at all: a type referencing itself recursed until the
    /// stack died, taking the process with it.
    /// </remarks>
    private const int MaxDepth = 12;

    /// <summary>
    /// Generates the schema for a type, as the root object of a structured-output request.
    /// </summary>
    public static JsonObject Generate(Type type)
    {
        if (type is null)
            throw new ArgumentNullException(nameof(type));

        return BuildNode(type, DescriptionOf(type), new Stack<Type>(), 0);
    }

    /// <summary>
    /// The schema as compact JSON, which is the form both provider SDKs take.
    /// </summary>
    public static string GenerateJson(Type type)
    {
        return Generate(type).ToJsonString();
    }

    private static JsonObject BuildNode(Type type, string? description, Stack<Type> ancestors, int depth)
    {
        if (depth > MaxDepth)
            throw new NotSupportedException(
                $"Schema generation exceeded {MaxDepth} levels at '{type.Name}'. This usually means a "
                + "type references itself; JSON Schema cannot express that here.");

        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (TryBuildScalar(underlying, description) is { } scalar)
            return scalar;

        if (underlying.IsEnum)
            return BuildEnum(underlying, description);

        if (TryGetElementType(underlying) is { } elementType)
            return BuildArray(elementType, description, ancestors, depth);

        return BuildObject(underlying, description, ancestors, depth);
    }

    private static JsonObject? TryBuildScalar(Type type, string? description)
    {
        var jsonType = type switch
        {
            _ when type == typeof(string) => "string",
            _ when type == typeof(bool) => "boolean",
            _ when type == typeof(int) || type == typeof(long) || type == typeof(short) => "integer",
            _ when type == typeof(float) || type == typeof(double) || type == typeof(decimal) => "number",

            // No JSON scalar for these - a string in a documented format is the only honest mapping.
            _ when type == typeof(DateTime) || type == typeof(DateTimeOffset) => "string",
            _ when type == typeof(Guid) => "string",
            _ => null
        };

        if (jsonType is null)
            return null;

        var node = new JsonObject { ["type"] = jsonType };

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            node["description"] = description ?? "ISO 8601 date-time string";
        else if (type == typeof(Guid))
            node["description"] = description ?? "GUID string";
        else if (description is not null)
            node["description"] = description;

        return node;
    }

    /// <summary>
    /// An enum becomes a string constrained to its member names.
    /// </summary>
    /// <remarks>
    /// Names rather than numbers because the number is meaningless to a model, and because both
    /// providers' strict modes express a closed set this way. That makes ResponseParser's
    /// string-enum converter part of the contract rather than a nicety: the default deserializer
    /// accepts only the number, so without it every enum here would parse back to nothing.
    /// </remarks>
    private static JsonObject BuildEnum(Type type, string? description)
    {
        var values = new JsonArray();

        foreach (var name in Enum.GetNames(type))
            values.Add(name);

        var node = new JsonObject
        {
            ["type"] = "string",
            ["enum"] = values,
            ["description"] = description ?? $"Enum of type {type.Name}"
        };

        return node;
    }

    private static JsonObject BuildArray(Type elementType, string? description, Stack<Type> ancestors, int depth)
    {
        // The item's own description comes from a [Description] on the element type - the only place
        // a type-level attribute is load-bearing, and something a consumer relies on today.
        var node = new JsonObject
        {
            ["type"] = "array",
            ["items"] = BuildNode(elementType, DescriptionOf(elementType), ancestors, depth + 1)
        };

        if (description is not null)
            node["description"] = description;

        return node;
    }

    private static JsonObject BuildObject(Type type, string? description, Stack<Type> ancestors, int depth)
    {
        if (ancestors.Contains(type))
            throw new NotSupportedException(
                $"'{type.Name}' refers to itself. Recursive types cannot be expressed in a strict "
                + "JSON schema - flatten the shape or split the call.");

        if (IsUnsupported(type, out var reason))
            throw new NotSupportedException(
                $"Cannot generate a schema for '{type.Name}': {reason}. Supported shapes are scalars, "
                + "enums, lists and arrays of those, and plain objects of the same.");

        ancestors.Push(type);

        try
        {
            var properties = new JsonObject();
            var required = new JsonArray();

            foreach (var property in Readable(type))
            {
                // Exactly as declared - see the case-sensitivity note on the class.
                var name = NameOf(property);

                properties[name] = BuildNode(
                    property.PropertyType, DescriptionOf(property), ancestors, depth + 1);

                // Strict mode requires every property listed. Optionality is expressed by the model
                // filling a field with an empty value, not by omitting it.
                required.Add(name);
            }

            return new JsonObject
            {
                ["type"] = "object",
                ["description"] = description ?? $"Object of type {type.Name}",
                ["properties"] = properties,
                ["required"] = required,
                ["additionalProperties"] = false
            };
        }
        finally
        {
            ancestors.Pop();
        }
    }

    /// <summary>
    /// The properties that become schema fields.
    /// </summary>
    /// <remarks>
    /// Setter-less properties are skipped. A computed property like <c>IsPass => Verdict == "pass"</c>
    /// cannot be deserialized into, so asking the model to produce one only spends tokens on a value
    /// that is then discarded - and invites it to contradict the field the value is derived from.
    /// The previous generator emitted them.
    /// </remarks>
    private static IEnumerable<PropertyInfo> Readable(Type type)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0)
                continue;

            if (!property.CanWrite)
                continue;

            if (property.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
                continue;

            yield return property;
        }
    }

    /// <summary>
    /// The element type if this is a list-like shape, otherwise null.
    /// </summary>
    /// <remarks>
    /// Deliberately narrow: arrays and the generic collection interfaces, not IEnumerable at large.
    /// string is IEnumerable&lt;char&gt; and would otherwise become an array of characters.
    /// </remarks>
    private static Type? TryGetElementType(Type type)
    {
        if (type == typeof(string))
            return null;

        if (type.IsArray)
            return type.GetElementType();

        if (!type.IsGenericType)
            return null;

        var definition = type.GetGenericTypeDefinition();

        var isListLike = definition == typeof(List<>)
                         || definition == typeof(IList<>)
                         || definition == typeof(IReadOnlyList<>)
                         || definition == typeof(ICollection<>)
                         || definition == typeof(IReadOnlyCollection<>)
                         || definition == typeof(IEnumerable<>);

        return isListLike ? type.GetGenericArguments()[0] : null;
    }

    private static bool IsUnsupported(Type type, out string reason)
    {
        if (typeof(System.Collections.IDictionary).IsAssignableFrom(type))
        {
            reason = "dictionaries have no fixed property set, which strict mode requires";
            return true;
        }

        if (type.IsAbstract || type.IsInterface)
        {
            reason = "abstract types and interfaces have no single concrete shape";
            return true;
        }

        reason = "";
        return false;
    }

    private static string NameOf(PropertyInfo property)
    {
        return property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;
    }

    private static string? DescriptionOf(MemberInfo member)
    {
        return member.GetCustomAttribute<DescriptionAttribute>()?.Description;
    }
}
