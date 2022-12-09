using System.Collections.Generic;
using System.Threading.Tasks;
using Ouroboros.Documents.Elements;

namespace Ouroboros.Documents;

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