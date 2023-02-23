using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Ouroboros.Builder;
using Ouroboros.Documents;
using Ouroboros.Documents.Extensions;
using Ouroboros.LargeLanguageModels;

[assembly: InternalsVisibleTo("Ouroboros.Test")]

namespace Ouroboros;

public class OuroClient : IAsker
{
    private readonly IApiClient ApiClient;

    public async Task<AskBuilder> Ask(string text, string newElementName = "")
    {
        var doc = new Document(this, text);
        var element = await doc.ResolveAndSubmit(newElementName);

        return new AskBuilder(doc, element.Text);
    }


    /// <summary>
    /// Sends the text string to the LLM for completion. This is the most direct route
    /// to completion and is ultimately the only place where we actually call the LLM.
    /// </summary>
    public async Task<string> SendForCompletion(string text)
    {
        return await ApiClient.Complete(text);
    }

    public Document CreateDocument(string text)
    {
        return new Document(this, text); // TODO: return an IDocument builder interface
    }

    public async Task<string> Resolve(string path)
    {
        var text = await System.IO.File.ReadAllTextAsync(path);

        var fragment = new Document(this, text);
        await fragment.Resolve();

        return fragment.ToString();
    }

    ///// <summary>
    ///// Resolves the next element, and then stops. 
    ///// </summary>
    //public async Task<Document> ResolveNext(string path)
    //{
    //    var text = await System.IO.File.ReadAllTextAsync(path);

    //    var doc = new Document(this, text);
        
    //    await doc.Resolve(new ResolveOptions()
    //    {
    //        HaltAfterFirstComplete = true
    //    });

    //    return (Document) doc;
    //}

    ///// <summary>
    ///// Resolves the next element, and then stops. 
    ///// </summary>
    //public async Task ResolveNext(Document document)
    //{
    //    await document.ResolveNext();
    //}

    public async Task<string> Summarize(string text, int maxSentences)
    {
        var fragment = new Document(this, 
            $"This is a brilliant Harvard business professor who summarizes the provided text into at most {maxSentences} sentences, " + 
            $"solving any spelling and grammatical issues. She preserves the original author's intent and does not censor criticism or add any new meaning." +
            $"If the text involves details that might be attributable to the author, she will remove those to protect the author." +
            "The result is professional and succinct, and cannot be traced to the original author in any way.\n\n" + 
            $"Text: {text}\n" +
            $"Summary:");

        await fragment.ResolveAndSubmit();
        var response = fragment.GetLastAsText();

        return response;
    }

    //public async Task<List<string>> Mine(string path)
    //{
    //    var text = await System.IO.File.ReadAllTextAsync(path);
    //    var client = new OpenAiClient(ApiKey);

    //    var sifter = new Sifter(client);

    //    return await sifter.Mine(text);
    //}

    public OuroClient(string apiKey)
    {
        ApiClient = new OpenAiClient(apiKey);
    }
}