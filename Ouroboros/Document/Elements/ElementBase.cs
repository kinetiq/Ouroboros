using System;

namespace Ouroboros.Document.Elements;

[Serializable]
internal class ElementBase
{
    /// <summary>
    /// You can optionally give any element a unique name. Ouroboros does not use this internally;
    /// it's used by devs only.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The text inside the element.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// If a tag's text contents has been summarized, that is stored here.
    /// </summary>
    public string TextSummary { get; set; } = string.Empty;

    public override string ToString()
    {
        return Text;
    }
}