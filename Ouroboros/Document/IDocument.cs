namespace Ouroboros.Document;

/// <summary>
/// A resolved or partially resolved document.
/// </summary>
public interface IDocument
{
    /// <summary>
    /// Returns the text representation of the document model.
    /// </summary>
    string ToString();
}