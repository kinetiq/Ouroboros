using AI.Dev.OpenAI.GPT;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.Completions;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OpenAI.Managers;
using static OpenAI.ObjectModels.SharedModels.IOpenAiModels;

[assembly: InternalsVisibleTo("Ouroboros.Test")]

namespace Ouroboros;

public class OuroClient 
{
    private readonly OpenAiClient ApiClient;
    private OuroModels? DefaultModel;
    
    public OpenAIService InnerClient => ApiClient.GetClient();

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
    public async Task<CompleteResponseBase> PromptToStringAsync(string prompt, CompleteOptions? options = null)
    {
        options = ConfigureOptions(options);

        var response = await ApiClient.CompleteAsync(prompt, options);

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



    public OuroClient(string apiKey)
    {
        ApiClient = new OpenAiClient(apiKey);
        DefaultModel = null;
    }
}