using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ouroboros.Extensions;

internal static class Extensions
{
    public static bool EqualsIgnoreCase(this string @this, string value)
    {
        return @this.Equals(value, StringComparison.InvariantCultureIgnoreCase);
    }

    /// <summary>
    /// True if @this is in the array of values.
    /// </summary>
    public static bool In<T>(this T @this, params T[] values)
    {
        return Array.IndexOf(values, @this) != -1;
    }

    public static void RemoveWhere<T>(this ICollection<T> @this, Func<T, bool> predicate)
    {
        var list = @this
            .Where(predicate)
            .ToList();

        foreach (var item in list)
            @this.Remove(item);
    }

    public static string RemoveWhere(this string @this, Func<char, bool> filter)
    {
        return new string(@this
            .ToCharArray()
            .Where(x => !filter(x))
            .ToArray());
    }

    /// <summary>
    /// Syntactic sugar for converting to title case.
    /// </summary>
    public static string ToTitleCase(this string @this)
    {
        return new CultureInfo("en-US")
            .TextInfo
            .ToTitleCase(@this);
    }

    /// <summary>
    /// Syntactic sugar for converting to title case.
    /// </summary>
    public static string ToTitleCase(this string @this, CultureInfo cultureInfo)
    {
        return cultureInfo
            .TextInfo
            .ToTitleCase(@this);
    }

    /// <summary>
    /// Syntactic sugar for checking if a string is null or empty.
    /// </summary>
    public static bool IsNullOrEmpty(this string @this)
    {
        return string.IsNullOrEmpty(@this);
    }

    /// <summary>
    /// Syntactic sugar for checking if a string is null or whitespace.
    /// </summary>
    public static bool IsNullOrWhiteSpace(this string value)
    {
        return string.IsNullOrWhiteSpace(value);
    }
}