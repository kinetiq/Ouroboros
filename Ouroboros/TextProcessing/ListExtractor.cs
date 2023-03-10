using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ouroboros.TextProcessing;

internal static class ListExtractor
{
    internal static List<string> GetList(string rawText)
    {
        // This turns a numbered list into a list of strings with no numbers.
        // TODO: detect numbers. If there are none, instead break on newlines.
        // TODO: option to preserve numbers, or return a list of tuples with numbers and strings.

        var pattern = @"([\d]+\. )(.*?)(?=([\d]+\.)|($))";
        var matches = Regex.Matches(rawText, pattern, RegexOptions.Singleline);

        if (matches.Count == 0)
            return new List<string>();

        // Create list of themes
        return matches.Select(x => x.Groups[2]
                .Value
                .Trim())
            .ToList();
    }
}