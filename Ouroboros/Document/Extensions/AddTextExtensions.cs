using Ouroboros.Document.Elements;

namespace Ouroboros.Document.Extensions;

internal static class AddTextExtensions
{
    public static TextElement AddText(this Document @this, string text)
    {
        var element = new TextElement(text);

        @this
            .DocElements
            .Add(element);

        return element;
    }
}