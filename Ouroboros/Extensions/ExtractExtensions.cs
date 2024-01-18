﻿using Ouroboros.Responses;
using Ouroboros.TextProcessing;
using System.Collections.Generic;
using Ouroboros.Enums;
using Z.Core.Extensions;
using System;

namespace Ouroboros.Extensions;

public static class ExtractExtensions
{
    public static string ExtractString(this OuroResponseBase @this)
    {
        // This works even if there are errors.
        return @this.ResponseText;
    }

    public static TEnum ExtractEnum<TEnum>(this OuroResponseBase @this) where TEnum : struct, Enum
    {
        var text = @this.ResponseText;

        if (string.IsNullOrWhiteSpace(text))
            return default;

        text = text.Trim().ToLower();

        foreach (var value in Enum.GetValues(typeof(TEnum)))
        {
            var stringValue  = value.ToString()!.ToLower();

            if (stringValue == "nomatch")
                continue;

            if (text.StartsWith(stringValue))
                return (TEnum)value;
        }

        return default;
    }

    public static YesNo ExtractYesNo(this OuroResponseBase @this)
    {
        return @this.ExtractEnum<YesNo>();
    }

    public static TrueFalse ExtractTrueFalse(this OuroResponseBase @this)
    {
        return @this.ExtractEnum<TrueFalse>();
    }

    /// <summary>
    /// Sends the chat payload for completion, then senses the list type and splits the text into a list.
    /// Works with numbered lists and lists separated by any type of newline. 
    /// </summary>
    public static List<ListItem> ExtractList(this OuroResponseBase @this)
    {
        return ListExtractor.Extract(@this.ResponseText);
    }

    /// <summary>
    /// Sends the chat payload for completion, then splits the result into a numbered list.
    /// Any item that doesn't start with a number is discarded. Note that this is different from SendAndExtractList
    /// in a few ways, including the result type, which in this case is able to include the item number (since these
    /// items are numbered).
    /// </summary>
    public static List<NumberedListItem> ExtractNumberedList(this OuroResponseBase @this)
    {
        return ListExtractor.ExtractNumbered(@this.ResponseText);
    }
}