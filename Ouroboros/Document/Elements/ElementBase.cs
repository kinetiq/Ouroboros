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
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// If a tag's content has been summarized, that is stored here.
    /// </summary>
    public string ContentSummary { get; set; } = string.Empty;

    public override string ToString()
    {
        return Content;
    }
}