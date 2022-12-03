using System.Collections.Generic;
using System.Linq;
using Ouroboros.Document.Elements;
using Z.Core.Extensions;

namespace Ouroboros.Document.Mutators;

internal class ResolverMutator
{
    private readonly ResolveElement Element;
    private List<ElementBase> DocumentModel;

    /// <summary>
    /// To prepare a new DeepFragment based on a resolver, we need to take our existing
    /// document model and change it. The Resolver element provides a prompt, and that
    /// has to replace the existing prompt tag. Also, the Resolve tag itself has to be
    /// removed, and any content it has must be added as text. 
    /// </summary>
    public List<ElementBase> Mutate()
    {
        // Cut the elements list off right before Element. 
        TrimElements();

        // Swap out the prompt tag with the one provided in Element. 
        SetupPrompt();

        // Add text to the end.
        AddTextElement();

        return DocumentModel;
    }

    public Document MutateToNewFragment()
    {
        Mutate();

        return new Document(DocumentModel);
    }

    #region Helpers
    /// <summary>
    /// Cut the elements list off right *after* this element. 
    /// </summary>
    private void TrimElements()
    {
        var index = DocumentModel
            .IndexOf(Element);

        DocumentModel = DocumentModel
            .Take(index)
            .ToList();
    }

    /// <summary>
    /// Swap out the prompt content with the content provided in our resolve tag.
    /// </summary>
    private void SetupPrompt()
    {
        if (Element.Prompt == null) // if this isn't specified, we stick with the original prompt.
            return;

        var prompt = DocumentModel
            .First(x => x is PromptElement);

        prompt.Content = Element.Prompt;
    }

    /// <summary>
    /// If the resolve tag has content, add that to the end of the document.
    /// </summary>
    private void AddTextElement()
    {
        var content = Element.Content;

        if (content.IsNullOrWhiteSpace())
            return;

        var textElement = new TextElement()
        {
            Content = content
        };

        DocumentModel.Add(textElement);
    } 
    #endregion

    public ResolverMutator(Document source, ResolveElement element)
    {
        DocumentModel = source
            .DocElements
            .DeepClone();

        // We just cloned the DocumentModel so that we can work on it without affecting the original.
        // Now we need to grab the version of our resolver element that exists in the new model.
        var sourceIndex = source
            .DocElements
            .IndexOf(element);

        Element = (ResolveElement) DocumentModel[sourceIndex];
    }
}