#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ouroboros.Document.Elements;
using Ouroboros.Document.Extensions;
using Ouroboros.Document.Factories;
using Ouroboros.Document.Mutators;
using Ouroboros.OpenAI;

namespace Ouroboros.Document;

internal class Document : IDocument
{
    private ResolveOptions Options = new ResolveOptions();

    /// <summary>
    /// Used to show the user where the last completion was.
    /// </summary>
    public ElementBase? LastResolvedElement { get; set; } = null;
    public List<ElementBase> DocElements { get; set; }

    #region Public API


    /// <summary>
    /// Resolves this document.
    /// </summary>
    public async Task Resolve(ResolveOptions? options = null)
    {
        Options = options ?? new ResolveOptions();

        // TODO: Any element could contain a resolve element. Maybe we should call them all recursively. 
        // TODO: For now, we only check resolve elements.
        var resolveElements = DocElements
            .OfType<ResolveElement>()
            .Where(x => !x.IsResolved)
            .ToList();

        // Iterate through any Resolve elements first. Handle these by calling the API. 
        foreach (var element in resolveElements)
        {
            await ResolveElement(element);
            LastResolvedElement = element;

            if (Options.HaltAfterFirstComplete)
                return;
        }

        // Submit to GPT if necessary.
        if (Options.SubmitResultForCompletion)
            await SubmitAndAppend();
    }

    /// <summary>
    /// Resolves this document, but stop after the first completion.
    /// </summary>
    /// <returns></returns>
    public async Task ResolveNext()
    {
        await Resolve(new ResolveOptions()
        {
            HaltAfterFirstComplete = true
        });
    }

    /// <summary>
    /// Override that always submits the document to the LLM. This is not the default behavior.
    /// </summary>
    public async Task<TextElement> ResolveAndSubmit(string newElementName = "")
    {
        await Resolve(new ResolveOptions()
        {
            SubmitResultForCompletion = true, 
            HaltAfterFirstComplete = Options.HaltAfterFirstComplete,
            NewElementName = newElementName 
        });

        return this.GetLastGeneratedAsElement();
    }

    /// <summary>
    /// Renders the document to text for use as input into an LLM.
    /// </summary>
    internal string ToModelInput()
    {
        var builder = new StringBuilder();

        foreach (var element in DocElements)
            builder.Append(element.ToModelInput());

        return builder.ToString();
    }

    /// <summary>
    /// Returns the text representation of the document model.
    /// </summary>
    public override string ToString()
    {
        var builder = new StringBuilder();

        foreach (var element in DocElements)
            builder.Append(element.ToString());

        return builder.ToString();
    }
    #endregion

    #region Resolution

    /// <summary>
    /// Submits the document to the LLM, and then appends the result onto the end.
    /// </summary>
    private async Task SubmitAndAppend()
    {
        var documentText = this.ToModelInput();

        var client = new Gpt3Client();
        var result = await client.Complete(documentText);

        var textElement = new TextElement()
        {
            Id = Options.NewElementName,
            IsGenerated = true,
            Text = result
        };

        DocElements.Add(textElement);
        LastResolvedElement = textElement;
    }

    /// <summary>
    /// Resolves a single element by (1) recursively resolving any Resolve tags it may have and then
    /// (2) resolving the entire resulting text (which, again, is just one element).
    ///
    /// Although we are only resolving a single element, the process involves the entire document
    /// leading up to and including it. It does get a bit involved.  
    /// 
    /// The text that comes out of that process becomes the GeneratedText of our element.
    /// </summary>
    private async Task ResolveElement(ResolveElement element)
    {
        // We can't perform the resolve directly on our element, because resolution involves
        // operations like swapping out the prompt, removing everything after element,
        // and so on. We don't want to permanently damage the original fragment.
        //
        // Instead, we create a new "workspace" fragment to help us handle this job. This is a
        // deep copy of the entire document fragment. 
        //
        // We use that workspace, chop it up, submit it to the API, and 
        // plug the result back into our original element. The workspace gets tossed.

        var mutator = new ResolverMutator(this, element);
        var workspace = mutator.MutateToNewFragment();

        //     ███ ███ ███ █┼█ ███ ███ ███ ███ █┼┼█     // 
        //     █▄┼ █▄┼ █┼┼ █┼█ █▄┼ █▄▄ ┼█┼ █┼█ ██▄█     // 
        //     █┼█ █▄▄ ███ ███ █┼█ ▄▄█ ▄█▄ █▄█ █┼██     //
        //            HERE THERE BE SERPENTS            //

        // Recursively resolve the element, then submit it. What we get back is an element that belongs
        // to the workspace fragment. That contains our results.  
        var newElement = await workspace.ResolveAndSubmit();

        // Plug that content into our element, and mark it resolved.
        element.GeneratedText = newElement.Text;
        element.IsResolved = true;
    }
    #endregion

    internal Document(List<ElementBase> docElements)
    {
        DocElements = docElements;
    }

    internal Document(string text) 
    {
        var factory = new DocElementsFactory();
        DocElements = factory.Create(text);
    }
}