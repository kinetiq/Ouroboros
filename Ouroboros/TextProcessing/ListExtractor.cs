using Ouroboros.Documents.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Z.Core.Extensions;

namespace Ouroboros.TextProcessing;

internal static class ListExtractor
{
    /// <summary>
    /// Given a block of text, senses the format, and splits it into a list of strings. Works with numbered lists (top priority) followed by lists separated
    /// by any type of newline. If it's a numbered list, it removes the numbers.
    /// </summary>
    internal static List<string> Extract(string rawText)
    {
        if(rawText.IsNullOrWhiteSpace())
            return new List<string>();

        rawText = rawText.Trim();

        return IsNumberedList(rawText) ? 
            ExtractNumberedList(rawText) : 
            ExtractNewLineList(rawText);
    }

    private static List<string> ExtractNewLineList(string rawText)
    {
        var lines = rawText
            .SplitTextOnNewLines(StringSplitOptions.RemoveEmptyEntries | 
                                 StringSplitOptions.TrimEntries)
            .ToList();

        if (IsStealthNumbered(lines))
            lines = RemoveStealthNumbering(lines);

        return lines;
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

    private static List<string> RemoveStealthNumbering(List<string> lines)
    {
        var result = new List<string>();
        var index = 1;

        foreach (var line in lines)
        {
            var currentLine = line;
            var expected = index.ToString() + ' ';

            if (line.StartsWith(expected))
                currentLine = currentLine[expected.Length..].Trim();

            if (currentLine.Length > 0)
                result.Add(currentLine);

            index++;
        }
        
        return result;
    }

    /// <summary>
    /// Extract the numbered list using a regex.
    /// </summary>
    private static List<string> ExtractNumberedList(string rawText)
    {
        var pattern = @"([\d]+\. )(.*?)(?=([\d]+\.)|($))";
        var matches = Regex.Matches(rawText, pattern, RegexOptions.Singleline);

        if (matches.Count == 0)
            return new List<string>();

        // Get the text part of the list. Toss the number.
        return matches
            .Select(x => x.Groups[2]
                .Value
                .Trim())
            .ToList();
    }
}