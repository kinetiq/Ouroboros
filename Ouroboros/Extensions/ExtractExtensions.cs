using Ouroboros.Responses;
using Ouroboros.TextProcessing;
using System.Collections.Generic;

namespace Ouroboros.Extensions;

public static class ExtractExtensions
{
    public static string ExtractString(this OuroResponseBase @this)
    {
        // This works even if there are errors.
        return @this.ResponseText;
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
    /// Any item that doesn't start with an number is discarded. Note that this is different than SendAndExtractList
    /// in a few ways, including the result type, which in this case is able to include the item number (since these
    /// items are numbered).
    /// </summary>
    public static List<NumberedListItem> ExtractNumberedList(this OuroResponseBase @this)
    {
        return ListExtractor.ExtractNumbered(@this.ResponseText);
    }
}