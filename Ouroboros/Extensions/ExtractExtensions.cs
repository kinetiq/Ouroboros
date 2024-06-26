﻿using System;
using System.Collections.Generic;
using Ouroboros.Enums;
using Ouroboros.Responses;
using Ouroboros.TextProcessing;

namespace Ouroboros.Extensions;

public static class ExtractExtensions
{
    public static string ExtractString(this OuroResponseBase @this)
    {
        // This works even if there are errors.
        return @this.ResponseText;
    }

    /// <summary>
    /// Extracts to an arbitrary enum. The enum must have a member called "NoMatch", or there will be an
    /// exception when a match fails.
    /// </summary>
    public static TEnum ExtractEnum<TEnum>(this OuroResponseBase @this) where TEnum : struct, Enum
    {
        var text = @this.ResponseText;

        return ProteusConvert.ToEnum<TEnum>(text);
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