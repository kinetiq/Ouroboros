using AI.Dev.OpenAI.GPT;
using OpenAI.GPT3.ObjectModels;
using Ouroboros.Builder;
using Ouroboros.Documents;
using Ouroboros.Events;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.Completions;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;


[assembly: InternalsVisibleTo("Ouroboros.Test")]

namespace Ouroboros;

public class OuroClient 
{
    private readonly IApiClient ApiClient;
    private OuroModels? DefaultModel;
    
    /// <summary>
    /// Start here if you want to chain several prompts together with multiple .Chain calls.
    /// These are not executed until you call one of the async methods, such as .AsDocumentAsync
    /// or .AsListAsync
    /// </summary>
    public ChainBuilder Prompt(string prompt, CompleteOptions? options = null)
    {
        options = ConfigureOptions(options);

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
    internal async Task<CompleteResponseBase> SendForCompletionAsync(string prompt, CompleteOptions? options = null)
    {
        options = ConfigureOptions(options);

        var response = await ApiClient.Complete(prompt, options);

        return response;
    }

    /// <summary>
    /// Configures a default model that will be used for all completions initiated from this client,
    /// unless overriden by passing in a model via CompleteOptions.
    /// </summary>
    public void SetDefaultModel(OuroModels model)
    {
        DefaultModel = model;
    }

    private CompleteOptions ConfigureOptions(CompleteOptions? options)
    {
        options ??= new CompleteOptions();
        options.Model ??= DefaultModel;

        return options;
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
        DefaultModel = null;
    }
}