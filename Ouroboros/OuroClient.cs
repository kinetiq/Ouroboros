using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AI.Dev.OpenAI.GPT;
using Ouroboros.Builder;
using Ouroboros.Documents;
using Ouroboros.LargeLanguageModels;

[assembly: InternalsVisibleTo("Ouroboros.Test")]

namespace Ouroboros;

public class OuroClient 
{
    private readonly IApiClient ApiClient;



    public ChainBuilder StartChain(string text, CompleteOptions? options = null)
    {
        var doc = new Document(this, text);

        return new ChainBuilder(doc, options);
    }

    /// <summary>
    /// Coverts text into tokens. Uses GPT3Tokenizer.
    /// </summary>
    public List<int> Tokenize(string text)
    {
        return GPT3Tokenizer.Encode(text);
    }

    /// <summary>
    /// Gets the number of tokens the given text would take up. Uses GPT3Tokenizer.
    /// </summary>
    public int TokenCount(string text)
    {
        var tokens = Tokenize(text);

        return tokens.Count;
    }

    /// <summary>
    /// Sends the text string to the LLM for completion. This is the most direct route
    /// to completion and is ultimately the only place where we actually call the LLM.
    /// </summary>
    public async Task<OuroResponseBase> SendForCompletion(string prompt, CompleteOptions? options = null)
    {
        return await ApiClient.Complete(prompt, options);
    }

    public Document CreateDocument(string text)
    {
        return new Document(this, text); 
    }

    //public async Task<string> Summarize(string text, int maxSentences)
    //{
    //    var fragment = new Document(this, 
    //        $"This is a brilliant Harvard business professor who summarizes the provided text into at most {maxSentences} sentences, " + 
    //        $"solving any spelling and grammatical issues. She preserves the original author's intent and does not censor criticism or add any new meaning." +
    //        $"If the text involves details that might be attributable to the author, she will remove those to protect the author." +
    //        "The result is professional and succinct, and cannot be traced to the original author in any way.\n\n" + 
    //        $"Text: {text}\n" +
    //        $"Summary:");

    //    await fragment.ResolveAndSubmit();
    //    var response = fragment.GetLastAsText();

    //    return response;
    //}

    public OuroClient(string apiKey)
    {
        ApiClient = new OpenAiClient(apiKey);
    }
}