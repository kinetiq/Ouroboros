using System;
using System.Linq;
using System.Reflection;
using Ouroboros.Enums;
using Ouroboros.Extensions;
using Ouroboros.Reflection;

namespace Ouroboros.TextProcessing;

public static class ProteusConvert
{
    public static bool ToBool(string text)
    {
        text = text.Trim().ToLower();

        if (text.In("yes", "y", "true", "t", "1"))
            return true;

        if (text.In("no", "n", "false", "f", "0"))
            return false;

        throw new ArgumentException("Invalid value for boolean conversion: " + text);
    }

    /// <summary>
    /// Attempts to convert text into an enum value. Matches are case-insensitive and can occur on the enum value or an alias
    /// via OuroAliasAttribute.
    /// </summary>
    public static T ToEnum<T>(string text) where T : struct, Enum
    {
        var type = typeof(T);

        if (!type.IsEnum)
            throw new ArgumentException("Type must be an enum.");

        if (string.IsNullOrWhiteSpace(text))
            return GetNoMatchOrThrow<T>(text);

        // We make two attempts at this. The first is a simple trim and match.
        // If that fails, we trim all values that cannot exist in an enum, and try again.

        // First Attempt
        var sanitized = text.Trim();

        var firstAttempt = FindMatchingEnumValue<T>(sanitized);

        if (firstAttempt != null)
            return firstAttempt.Value;

        // Second Attempt
        sanitized = text.ReduceToValidNameCharacters();

        var secondAttempt = FindMatchingEnumValue<T>(sanitized);

        if (secondAttempt != null)
            return secondAttempt.Value;

        // Failing both attempts, we get the NoMatch value. 
        return GetNoMatchOrThrow<T>(text);
    }

    /// <summary>
    /// Find the enum value that corresponds to text. If no match is found,
    /// we have an Alias mechanism based on attributes. Try that as well.
    /// </summary>
    private static T? FindMatchingEnumValue<T>(string text) where T : struct, Enum
    {
        // Try to parse the enum value. If it fails, look for an alias.
        if (Enum.TryParse(text, true, out T enumValue))
            return enumValue;

        var field = Helpers.GetFieldWithAttribute<T, AliasAttribute>(
                x => x.Aliases.Any(alias => alias.Equals(text, StringComparison.InvariantCultureIgnoreCase))
            );

        if (field != null)
            return Enum.Parse<T>(field.Name);

        return null;
    }


    /// <summary>
    /// Attempts to convert text into an enum value. Matches are case-insensitive, and Alias matching is not supported.
    /// This overload is probably less efficient than the generic version, but it's useful if you don't know the enum type at
    /// compile time.
    /// </summary>
    public static object ToEnum(Type enumType, string text)
    {
        text = text.ReduceToValidNameCharacters();

        if (!enumType.IsEnum)
            throw new ArgumentException("Provided type must be an enum.", nameof(enumType));

        return GetValueGeneric(enumType, text);
    }

    /// <summary>
    /// Uses reflection to get the generic version of ToEnum and invoke it.
    /// </summary>
    private static object GetValueGeneric(Type enumType, string text)
    {
        var methodInfo = typeof(ProteusConvert).GetMethod(
            nameof(ToEnum),
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(string) },
            null)!;

        var genericMethodInfo = methodInfo.MakeGenericMethod(enumType);

        return genericMethodInfo.Invoke(null, new object[] { text })!;
    }


    private static T GetNoMatchOrThrow<T>(string text) where T : struct, Enum
    {
        var type = typeof(T);
        var noMatchValue = Helpers.GetNoMatchValue(type);

        if (noMatchValue != null)
            return (T)noMatchValue;

        throw new NotImplementedException(
            $"While binding enum {typeof(T).Name}, encountered an enum value that didn't map. " +
            $"The wasn't a NoMatch element, and there was no OuroNoMatch attribute. Cannot continue. " +
            $"The string value we tried to bind was: {text}");
    }
}