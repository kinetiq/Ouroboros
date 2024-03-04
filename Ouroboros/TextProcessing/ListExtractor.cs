using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Ouroboros.Extensions;

namespace Ouroboros.TextProcessing;

public static class ListExtractor
{
    /// <summary>
    /// Given a block of text, senses the format, and splits the block into a list. Works with numbered lists (top priority)
    /// followed by lists separated by any type of newline.
    /// </summary>
    public static List<ListItem> Extract(string rawText)
    {
        if (rawText.IsNullOrWhiteSpace())
            return new List<ListItem>();

        rawText = rawText.Trim();

        return IsNumberedList(rawText) ? ExtractNumberedList(rawText).ToList<ListItem>() : ExtractNewLineList(rawText);
    }

    /// <summary>
    /// Like Extract, except this discards any item that doesn't start with a number.
    /// This allows us to directly return NumberedListItem.
    /// </summary>
    public static List<NumberedListItem> ExtractNumbered(string rawText)
    {
        var items = Extract(rawText);

        items.RemoveWhere(x => x is not NumberedListItem);

        return items
            .Cast<NumberedListItem>()
            .ToList();
    }

    /// <summary>
    /// If we don't have a numbered list, we look for newlines and try splitting on those.
    /// Empty lines are discarded.
    /// </summary>
    private static List<ListItem> ExtractNewLineList(string rawText)
    {
        var lines = rawText
            .SplitTextOnNewLines(StringSplitOptions.RemoveEmptyEntries |
                                 StringSplitOptions.TrimEntries)
            .ToList();

        return lines
            .Select(x => new ListItem(x))
            .ToList();
    }

    /// <summary>
    /// The main style of numbered lists we're after is in the format of 1. [text]
    /// However, there's some other code in ExtractNewLineList that will try to catch situations where the dot is missing.
    /// </summary>
    private static bool IsNumberedList(string rawText)
    {
        const string pattern = @"^\d+(\.| |$)";

        return Regex.IsMatch(rawText, pattern);
    }

    /// <summary>
    /// Extract the numbered list using a regex.
    /// </summary>
    private static List<NumberedListItem> ExtractNumberedList(string rawText)
    {
        const string pattern = @"([\d]+(\. | |\.? ?$))(.*?)(?=((\r|\n)[\d]+(\. | |$))|($))";
        var matches = Regex.Matches(rawText, pattern, RegexOptions.Singleline);

        if (matches.Count == 0)
            return new List<NumberedListItem>();

        // Get the text part of the list. Toss the number.
        return matches
            .Select(x =>
                new NumberedListItem(ExtractInt(x.Groups[1].Value),
                    x.Groups[3].Value.Trim()))
            .ToList();
    }

    /// <summary>
    /// Value should be an integer followed by a dot or space. If this runs, the integer
    /// should always be present.
    /// </summary>
    private static int ExtractInt(string value)
    {
        // Get everything up to the first dot or space.
        var dotIndex = value.IndexOfAny(new[] { '.', ' ' });

        // This could only happen if we've got a number with no text at all.
        if (dotIndex == -1)
            return int.Parse(value);

        var number = value[..dotIndex];

        if (number == null)
            throw new InvalidOperationException("Called ExtractInt, but was unable to find the number: " + value);

        // Parse it to an int, if we can.
        return int.Parse(number);
    }
}