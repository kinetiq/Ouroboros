using System;
using System.Linq;
using System.Threading.Tasks;
using OpenAI.GPT3;
using OpenAI.GPT3.Interfaces;
using OpenAI.GPT3.Managers;
using OpenAI.GPT3.ObjectModels.ResponseModels;
using Ouroboros.LargeLanguageModels.OpenAI;

namespace Ouroboros.LargeLanguageModels;

internal class OpenAiClient : IApiClient
{
    private readonly string ApiKey;

    public async Task<OuroResponseBase> Complete(string prompt, CompleteOptions? options)
    {
        options ??= new CompleteOptions();

        var api = GetClient();

        var request = Mappings.MapOptions(prompt, options);
        var model = Mappings.MapModel(options.Model);

        var completionResult = await api.Completions.Create(request, model);

        if (completionResult.Successful)
            return GetResponseText(completionResult);

        return GetError(completionResult);
    }

    /// <summary>
    /// Extracts the ResponseText from from a completion response we already know to be
    /// successful.
    /// </summary>
    private static OuroResponseSuccess GetResponseText(CompletionCreateResponse completionResult)
    {
        if (!completionResult.Successful)
            throw new InvalidOperationException("Called GetResponseText on a response that was not marked successful. This should never happen.");
        
        var responseText = completionResult
            .Choices
            .First()
            .Text
            .Trim();

        return new OuroResponseSuccess(responseText);
    }

    private static OuroResponseFailure GetError(CompletionCreateResponse completionResult)
    {
        if (completionResult.Successful)
            throw new InvalidOperationException("Called GetError on a response that was successful. This should never happen.");

        var error = completionResult.Error == null
                   ? "Unknown Error"
                   : $"{completionResult.Error.Code}: {completionResult.Error.Message}";

        return new OuroResponseFailure(error);
    } 

    private OpenAIService GetClient()
    {
        return new OpenAIService(new OpenAiOptions
        {
            ApiKey = ApiKey
        });
    }

    internal OpenAiClient(string apiKey)
    {
        ApiKey = apiKey;
    }
}