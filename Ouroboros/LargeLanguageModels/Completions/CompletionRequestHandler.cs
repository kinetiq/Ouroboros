using OpenAI.Managers;
using System;
using System.Linq;
using System.Threading.Tasks;
using OpenAI.ObjectModels.ResponseModels;

namespace Ouroboros.LargeLanguageModels.Completions;
internal class CompletionRequestHandler
{
    private readonly OpenAIService Api;

    public async Task<CompleteResponseBase> Complete(string prompt, CompleteOptions? options)
    {
        options ??= new CompleteOptions();

        var request = Mappings.MapOptions(prompt, options);

        var completionResult = await Api.Completions.CreateCompletion(request);

        if (completionResult.Successful)
            return GetResponseText(completionResult);

        return GetError(completionResult);
    }

    /// <summary>
    /// Extracts the ResponseText from a completion response we already know to be
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

        return new CompleteResponseSuccess(responseText)
        {
            TotalTokenUsage = response.Usage.TotalTokens
        };
    }

    private static CompleteResponseFailure GetError(CompletionCreateResponse completionResult)
    {
        if (completionResult.Successful)
            throw new InvalidOperationException("Called GetError on a response that was successful. This should never happen.");

        var error = completionResult.Error == null
            ? "Unknown Error"
            : $"{completionResult.Error.Code}: {completionResult.Error.Message}";

        return new CompleteResponseFailure(error);
    }

    public CompletionRequestHandler(OpenAIService api)
    {
        Api = api;
    }
}
