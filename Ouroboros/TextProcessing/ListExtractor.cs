using Ouroboros.Documents.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text.RegularExpressions;
using Z.Collections.Extensions;
using Z.Core.Extensions;

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

        return IsNumberedList(rawText) ? 
            ExtractNumberedList(rawText).ToList<ListItem>() : 
            ExtractNewLineList(rawText);
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

    private static List<ListItem> ExtractNewLineList(string rawText)
    {
        var lines = rawText
            .SplitTextOnNewLines(StringSplitOptions.RemoveEmptyEntries |
                                 StringSplitOptions.TrimEntries)
            .ToList();

        if (IsStealthNumbered(lines))
            return HandleStealthNumbering(lines);

        return lines
            .Select(x => new ListItem(x))
            .ToList();
    }

    /// <summary>
    /// The main style of numbered list we're after is in the format of 1. [text]
    /// However, there's some other code in ExtractNewLineList that will try to catch situations where the dot is missing.
    /// </summary>
    private static bool IsNumberedList(string rawText)
    {
        return (rawText.Length >= 2 &&
                rawText[0].IsNumber() &&
                rawText[1] == '.');
    }

    /// <summary>
    /// Could happen if the list items are numbered, but don't have a dot.
    /// </summary>
    private static bool IsStealthNumbered(List<string> lines)
    {
        // Ensure we have some lines.
        if (!lines.Any())
            return false;

        // Ensure all lines start with a number,
        if (!lines.All(x => x[0].IsNumber()))
            return false;

        var index = 1;

        // Iterate the list, confirming we have numbers and they aren't random,
        // but follow a linear sequence starting with 1.
        foreach (var line in lines)
        {
            var expected = index.ToString() + ' ';

            if (!line.StartsWith(expected))
                return false;

            index++;
        }

        return true;
    }

    private static List<ListItem> HandleStealthNumbering(List<string> lines)
    {
        var result = new List<ListItem>();
        var index = 1;

        // TODO: this only works if the numbering is sequential from 1 to n.

        foreach (var line in lines)
        {
            var currentLine = line;
            var expected = index.ToString() + ' ';

            if (line.StartsWith(expected))
                currentLine = currentLine[expected.Length..].Trim();

            if (currentLine.Length > 0)
                result.Add(new NumberedListItem(index, currentLine));

            index++;
        }

        return result;
    }

    /// <summary>
    /// Extract the numbered list using a regex.
    /// </summary>
    private static List<NumberedListItem> ExtractNumberedList(string rawText)
    {
        const string pattern = @"([\d]+\. )(.*?)(?=([\d]+\.)|($))";
        var matches = Regex.Matches(rawText, pattern, RegexOptions.Singleline);

        if (matches.Count == 0)
            return new List<NumberedListItem>();

        // Get the text part of the list. Toss the number.
        return matches
            .Select(x => 
                new NumberedListItem(index: ExtractInt(x.Groups[1].Value), 
                                     text: x.Groups[2].Value.Trim()))
            .ToList();
    }

    /// <summary>
    /// Value should be an integer followed by a dot, although we will also allow
    /// a space. If it's an integer, convert it and return. Otherwise, return null.
    /// </summary>
    private static int ExtractInt(string value)
    {
        // Get everything up to the first dot or space.
        var dotIndex = value.IndexOfAny(new char[] { '.', ' ' });

        var number = value[..dotIndex];

        if (number == null)
            throw new InvalidOperationException("Called ExtractInt, but was unable to find the number: " + value);

        // Parse it to an int, if we can.
        return int.Parse(number);
    }
}