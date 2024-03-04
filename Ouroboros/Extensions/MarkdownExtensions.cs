namespace Ouroboros.Extensions;

public static class MarkdownExtensions
{
    public static bool IsMarkdownHeading(this string @this)
    {
        if (@this.IsNullOrWhiteSpace())
            return false;

        @this = @this.Trim();

        if (!@this.StartsWith("#"))
            return false;

        var header = @this.ExtractHeadingText();

        return header.Length > 0;
    }

    public static string ExtractHeadingText(this string @this)
    {
        var line = @this.Trim();

        // Skip any number of # characters at the start of the line and return the text.
        return line.TrimStart('#')
            .Trim();
    }
}