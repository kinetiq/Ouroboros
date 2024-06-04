using System;
using System.Linq;

namespace Ouroboros.Extensions;

public static class TextManipulationExtensions
{
    /// <summary>
    /// Uses a flexible approach that should work with various platforms.
    /// </summary>
    public static string[] SplitTextOnNewLines(this string @this, StringSplitOptions options = StringSplitOptions.None)
    {
        return @this
            .Split(
                new[] { "\r\n", "\r", "\n" },  // flexible approach
                options);
    }

    /// <summary>
    /// Reduces the string to contain only valid characters that can appear in a variable name.
    /// </summary>
    /// <param name="this">The input string.</param>
    /// <returns>The string with only valid characters.</returns>
    public static string ReduceToValidNameCharacters(this string @this)
    {
        return new string(@this
            .Where(c => char.IsLetterOrDigit(c) || c == '_')
            .ToArray());
    }
}
