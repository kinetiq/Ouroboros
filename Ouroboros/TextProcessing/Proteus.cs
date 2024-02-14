using System;
using System.Linq;
using System.Reflection;
using Ouroboros.Enums;
using Ouroboros.Reflection;
using Z.Core.Extensions;

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
    /// Attempts to convert text into an enum value. Matches are case-insensitive and can occur on the enum value or an alias via OuroAliasAttribute.
    /// </summary>
    
    public static T ToEnum<T>(string text) where T : struct, Enum
    {
        var type = typeof(T);

        if (!type.IsEnum)
            throw new ArgumentException("Type must be an enum.");

        if (string.IsNullOrWhiteSpace(text))
            return GetNoMatchOrThrow<T>(text);

        text = text.Trim();

        // Try to parse the enum value. If it fails, look for an alias.
        if (Enum.TryParse(text, ignoreCase: true, out T enumValue))
            return enumValue;

        var field = Helpers.GetFieldWithAttribute<T, AliasAttribute>(x => x.Alias.Equals(text, StringComparison.InvariantCultureIgnoreCase));

        if (field != null)
            return Enum.Parse<T>(field.Name);

        return GetNoMatchOrThrow<T>(text);
    }

    /// <summary>
    /// Attempts to convert text into an enum value. Matches are case-insensitive and can occur on the enum value or an alias via OuroAliasAttribute.
    /// This overload is probably less efficient than the generic version, but it's useful if you don't know the enum type at compile time.
    /// </summary>
    public static object ToEnum(Type enumType, string text)
    {
        if (!enumType.IsEnum)
            throw new ArgumentException("Provided type must be an enum.", nameof(enumType));

        MethodInfo methodInfo = typeof(ProteusConvert).GetMethod(
            nameof(ToEnum),
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(string) },
            null)!;

        MethodInfo genericMethodInfo = methodInfo.MakeGenericMethod(enumType);

        return genericMethodInfo.Invoke(null, new object[] { text })!;
    }


    private static T GetNoMatchOrThrow<T>(string text) where T : struct, Enum
    {
        var type = typeof(T);
        var noMatchValue = Helpers.GetNoMatchValue(type);

        if (noMatchValue != null)
            return (T) noMatchValue;
        
        throw new NotImplementedException($"While binding enum {typeof(T).Name}, encountered an enum value that didn't map. " +
                                          $"The wasn't a NoMatch element, and there was no OuroNoMatch attribute. Cannot continue. " +
                                          $"The string value we tried to bind was: {text}");
    }


}
