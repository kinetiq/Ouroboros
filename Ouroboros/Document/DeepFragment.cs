#nullable enable
using System.Linq;
using System.Threading.Tasks;
using Ouroboros.Document.Elements;
using Ouroboros.Document.Factories;
using Ouroboros.Document.Mutators;
using Ouroboros.OpenAI;

namespace Ouroboros.Document;

internal class DeepFragment
{
    private readonly DocumentModel Document;

    public async Task<DocumentModel> Resolve(ResolveOptions? options = null)
    {
        options ??= new ResolveOptions();

        var resolveElements = Document
            .Elements
            .OfType<ResolveElement>()
            .ToList();

        // Iterate through any resolve elements first. Resolve them by calling GPT. 
        foreach (var element in resolveElements)
            await ResolveElement(element);

        // Submit to GPT if necessary.
        if (options.SubmitResultForCompletion) 
            await SubmitAndAppend();
        
        return Document;
    }

    private async Task SubmitAndAppend()
    {
        var documentText = Document.ToString();

        var client = new Gpt3Client();
        var result = await client.Complete(documentText);

        var textElement = new TextElement()
        {
            Content = result
        };

        Document.Elements.Add(textElement);
    }

    private async Task ResolveElement(ResolveElement element)
    {
        // Create a DOM to help us resolve the resolve tag. We swap out the prompt and
        // cut the document off just before the resolve tag.

        var mutator = new ResolverDocumentMutator(Document, element);
        var dom = mutator.Mutate();

        var fragment = new DeepFragment(dom);

        // -**-!RECURSION!-**-
        var resultDom = await fragment.Resolve(
            new ResolveOptions()
            {
                SubmitResultForCompletion = true
            }
        );

        // Grab the last document from the DOM, that's the generated content. 
        var newElement = resultDom
            .Elements
            .Last();

        // Plug that content into our element, and mark it resolved.
        element.FullText = element.Content + newElement;
        element.IsResolved = true;
    }

    internal DeepFragment(DocumentModel model)
    {
        Document = model;
    }

    internal DeepFragment(string text)
    {
        var factory = new DocumentModelFactory();
        Document = factory.Create(text);
    }
}

internal class ResolveOptions
{
    public bool SubmitResultForCompletion { get; set; } = false;
}