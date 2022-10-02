using System.Linq;
using Ouroboros.Document.Elements;
using Z.Core.Extensions;

namespace Ouroboros.Document.Mutators;

internal class ResolverDocumentMutator
{
    private readonly DocumentModel Source;
    private readonly ResolveElement Element;

    /// <summary>
    /// To prepare a new DeepFragment based on a resolver, we need to take our existing
    /// DocumentModel and change it. The Resolver element provides a prompt, and that
    /// has to replace the existing prompt tag. Also, the Resolve tag itself has to be
    /// removed, and any content it has must be added as text. 
    /// </summary>
    public DocumentModel Mutate()
    {
        var newModel = Source.DeepClone();

        // Cut the elements list off right before Element. 
        TrimElements(newModel);

        // Swap out the prompt tag with the one provided in Element. 
        SetupPrompt(newModel);

        // Add text to the end.
        AddTextElement(newModel);

        return newModel;
    }

    #region Helpers
    /// <summary>
    /// Cut the elements list off right *before* this element. 
    /// </summary>
    private void TrimElements(DocumentModel newModel)
    {
        var index = Source.Elements.IndexOf(Element);

        newModel.Elements = newModel
            .Elements
            .Take(index)
            .ToList();
    }

    /// <summary>
    /// Swap out the prompt content with the content provided in our resolve tag.
    /// </summary>
    private void SetupPrompt(DocumentModel newModel)
    {
        var prompt = newModel
            .Elements
            .First(x => x is PromptElement);

        prompt.Content = Element.Prompt;
    }

    /// <summary>
    /// If the resolve tag has content, add that to the end of the document.
    /// </summary>
    private void AddTextElement(DocumentModel newModel)
    {
        var content = Element.Content;

        if (content.IsNullOrWhiteSpace())
            return;

        var textElement = new TextElement()
        {
            Content = content
        };

        newModel.Elements.Add(textElement);
    } 
    #endregion

    public ResolverDocumentMutator(DocumentModel source, ResolveElement element)
    {
        Source = source;
        Element = element;
    }
}