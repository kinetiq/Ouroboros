using AI.Dev.OpenAI.GPT;
using Microsoft.VisualStudio.Threading;
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
        
    public event AsyncEventHandler<OnRequestCompletedArgs>? OnRequestCompleted;

    /// <summary>
    /// Start here if you want to chain several prompts together with multiple .Chain calls.
    /// These are not executed until you call one of the async methods, such as .AsDocumentAsync
    /// or .AsListAsync
    /// </summary>
    public ChainBuilder Prompt(string prompt, CompleteOptions? options = null)
    {
        var doc = new Document(this, prompt);

        return new ChainBuilder(doc, options);
    }

    /// <summary>
    /// Syntactic sugar for cases where you want to quickly complete a prompt and get back the results. 
    /// </summary>
    public async Task<PromptResponse<string>> PromptToStringAsync(string prompt, CompleteOptions? options = null)
    {
        return await Prompt(prompt, options)
            .CompleteToStringAsync();
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
    internal async Task<OuroResponseBase> SendForCompletionAsync(string prompt, CompleteOptions? options = null)
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

        // Deliberately call this in a way that is not "fire and forget" because we want to run EF
        // code to save all our requests, and it might be best to avoid race conditions.
        if (OnRequestCompleted is not null) await OnRequestCompleted.InvokeAsync(this, args);

        return response;
    }

    /// <summary>
    /// **In almost all cases, you should start with Prompt, not this.** This creates a new Document from the prompt.
    /// It offers the most control, but is also the most verbose. Only necessary when you want to create a prompt and
    /// then manipulate the DOM before sending it to the LLM for completion.
    /// </summary>
    public Document CreateDocument(string prompt)
    {
        return new Document(this, prompt); 
    }

    public OuroClient(string apiKey)
    {
        ApiClient = new OpenAiClient(apiKey);
    }
}