using Ouroboros.Document.Elements;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ouroboros.Document;

/// <summary>
/// A resolved or partially resolved document.
/// </summary>
public interface IDocument
{
    /// <summary>
    /// Returns the text representation of the document model, for use in 
    /// </summary>
    string ToString();

    ElementBase? LastResolvedElement { get; set; }
    List<ElementBase> DocElements { get; set; }
    
    public Task ResolveNext();
}