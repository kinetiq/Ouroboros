using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenAI.GPT3;
using OpenAI.GPT3.Interfaces;
using OpenAI.GPT3.Managers;
using OpenAI.GPT3.ObjectModels.RequestModels;
using OpenAI.GPT3.ObjectModels;
using OpenAI.GPT3.ObjectModels.ResponseModels;
using Ouroboros.LargeLanguageModels.OpenAI;
using Ouroboros.LargeLanguageModels.Embeddings;
using Ouroboros.LargeLanguageModels.Completions;

namespace Ouroboros.LargeLanguageModels;

internal class OpenAiClient : IApiClient
{
    private readonly string ApiKey;

    #region Complete
    public async Task<CompleteResponseBase> Complete(string prompt, CompleteOptions? options)
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
    private static CompleteResponseSuccess GetResponseText(CompletionCreateResponse response)
    {
        if (!response.Successful)
            throw new InvalidOperationException("Called GetResponseText on a response that was not marked successful. This should never happen.");

        var responseText = response
            .Choices
            .First()
            .Text
            .Trim();

        return new CompleteResponseSuccess(responseText);
    } 
    #endregion

    private static CompleteResponseFailure GetError(CompletionCreateResponse completionResult)
    {
        if (completionResult.Successful)
            throw new InvalidOperationException("Called GetError on a response that was successful. This should never happen.");

        var error = completionResult.Error == null
            ? "Unknown Error"
            : $"{completionResult.Error.Code}: {completionResult.Error.Message}";

        return new CompleteResponseFailure(error);
    }

    #region Embeddings
    public async Task<EmbeddingResponseBase> RequestEmbeddings(List<string> inputs, OuroModels model)
    {
        var openAiModel = Mappings.MapModel(model);
        var modelName = openAiModel.EnumToString();

        var api = GetClient();

        var request = new EmbeddingCreateRequest()
        {
            InputAsList = inputs,
            Model = modelName
        };

        var result = await api.CreateEmbedding(request);

        if (result.Successful)
            return GetEmbeddings(result, inputs);

        return GetError(result);
    }

    /// <summary>
    /// Extracts the ResponseText from from a completion response we already know to be
    /// successful.
    /// </summary>
    private static EmbeddingResponseSuccess GetEmbeddings(EmbeddingCreateResponse response, List<string> inputs)
    {
        if (!response.Successful)
            throw new InvalidOperationException("Called GetEmbeddings on a response that was not marked successful. This should never happen.");

        var embeddings = response
            .Data
            .Select(x => new OuroEmbedding()
            {
                Embedding = x.Embedding,
                Index = x.Index,
                Original = inputs[x.Index ?? 0] // if index is null, I assume that means there was only one input?   
            })
            .ToList();

        return new EmbeddingResponseSuccess(embeddings);
    }

    private static EmbeddingResponseFailure GetError(EmbeddingCreateResponse completionResult)
    {
        if (completionResult.Successful)
            throw new InvalidOperationException("Called GetError on a response that was successful. This should never happen.");

        var error = completionResult.Error == null
            ? "Unknown Error"
            : $"{completionResult.Error?.Code ?? "Unknown Error"}: {completionResult.Error?.Message ?? "No Message"}";

        return new EmbeddingResponseFailure(error);
    } 
    #endregion



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