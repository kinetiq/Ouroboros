using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Ouroboros.Document;
using Ouroboros.Document.Extensions;
using Ouroboros.Scales;
using Ouroboros.VulcanMiner;

[assembly: InternalsVisibleTo("Ouroboros.Test")]

namespace Ouroboros;

public class Client
{
    //private string TemplateRoot { get; set; }

    public async Task<string> Resolve(string path)
    {
        var text = await System.IO.File.ReadAllTextAsync(path);

        var fragment = new Document.Document(text);
        await fragment.Resolve();

        return fragment.ToString();
    }

    /// <summary>
    /// Resolves the next element, and then stops. 
    /// </summary>
    public async Task<IDocument> ResolveNext(string path)
    {
        var text = await System.IO.File.ReadAllTextAsync(path);

        var doc = new Document.Document(text);

        await doc.Resolve(new ResolveOptions()
        {
            HaltAfterFirstComplete = true
        });

        return (IDocument) doc;
    }

    /// <summary>
    /// Resolves the next element, and then stops. 
    /// </summary>
    public async Task ResolveNext(IDocument document)
    {
        await document.ResolveNext();
    }



    public async Task<string> Summarize(string text, int maxSentences)
    {
        var fragment = new Document.Document(
            $"This is a Harvard business professor who summarizes the provided text into at most {maxSentences} sentences, " + 
            $"solving any spelling and grammatical issues. She preserves the original author's intent and does not censor criticism or add any new meaning." +
            $"If the text involves details that might be attributable to the author, the professor will remove those to protect the author." +
            "The result is professional and succinct, and cannot be traced to the original author in any way.\n\n" + 
            $"Text: {text}\n" +
            $"Summary:");

        await fragment.ResolveAndSubmit();
        var response = fragment.GetLastAsText();

        return response;
    }

    public async Task<List<string>> Mine(string path)
    {
        var text = await System.IO.File.ReadAllTextAsync(path);
        var sifter = new Sifter();

        return await sifter.Mine(text);
    }
}