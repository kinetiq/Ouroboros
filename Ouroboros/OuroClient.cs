using AI.Dev.OpenAI.GPT;
using Ouroboros.Builder;
using Ouroboros.Documents;
using Ouroboros.Events;
using Ouroboros.LargeLanguageModels;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("Ouroboros.Test")]

namespace Ouroboros;

public class OuroClient 
{
    private readonly IApiClient ApiClient;
    public event EventHandler<OnRequestCompletedArgs>? OnRequestCompleted;

    /// <summary>
    /// Start here if you want to chain several prompts together with multiple .Chain calls.
    /// These are not executed until you call one of the async methods, such as .AsDocumentAsync
    /// or .AsListAsync
    /// </summary>
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
        var response = await ApiClient.Complete(prompt, options);

        // Fire the OnRequestCompleted, which allows the user to tie directly into the pipeline of requests
        // and easily log them even in chaining scenarios.
        var promptTokens = TokenCount(prompt);
        var responseTokens = response is OuroResponseSuccess ? TokenCount(response.ResponseText) : 0;

        var args = new OnRequestCompletedArgs()
        {
            Prompt = prompt,
            Response = response,
            Tokens = promptTokens + responseTokens
        };

        OnRequestCompleted?.Invoke(this, args);

        return response;
    }

    public Document CreateDocument(string text)
    {
        return new Document(this, text); 
    }

    public OuroClient(string apiKey)
    {
        ApiClient = new OpenAiClient(apiKey);
    }
}