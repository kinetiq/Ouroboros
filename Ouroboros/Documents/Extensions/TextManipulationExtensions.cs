using System;

namespace Ouroboros.Documents.Extensions;

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
}