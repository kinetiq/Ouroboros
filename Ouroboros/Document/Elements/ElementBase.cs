using System;

namespace Ouroboros.Document.Elements;

[Serializable]
public abstract class ElementBase
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

    public abstract string Type();

    /// <summary>
    /// If a tag's text contents has been summarized, that is stored here.
    /// </summary>
    public string TextSummary { get; set; } = string.Empty;

    /// <summary>
    /// For use when displaying an element to the user.
    /// </summary>
    public override string ToString()
    {
        return Text;
    }

    /// <summary>
    /// For use when rendering an element as input into an LLM API.
    /// </summary>
    internal virtual string ToModelInput()
    {
        return Text;
    }
}