using OpenAI.Managers;
using OpenAI.ObjectModels.ResponseModels;
using Ouroboros.LargeLanguageModels.Resilience;
using Ouroboros.Responses;
using Polly;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Ouroboros.LargeLanguageModels.Completions;
internal class CompletionRequestHandler
{
    private readonly OpenAIService Api;

    public async Task<OuroResponseBase> Complete(string prompt, CompleteOptions? options)
    {
        options ??= new CompleteOptions();

        var request = Mappings.MapOptions(prompt, options);

        var delay = BackoffPolicy.GetBackoffPolicy(options.UseExponentialBackOff);

        // TODO: consider more nuanced error handling: https://platform.openai.com/docs/guides/error-codes/api-errors

        var result = await Policy
            .Handle<Exception>()
            .OrResult<CompletionCreateResponse>(x => x == null || !x.Successful)
            .WaitAndRetryAsync(delay)
            .ExecuteAndCaptureAsync(async () => await Api.Completions.CreateCompletion(request));

        if (result.Outcome == OutcomeType.Successful)
            return GetResponseText(result.Result!);

        return result switch
        {
            { FaultType: FaultType.ExceptionHandledByThisPolicy } => new OuroResponseFailure(result.FinalException!.Message),
            { FaultType: FaultType.ResultHandledByThisPolicy } => GetError(result.FinalHandledResult!),
            _ => throw new InvalidOperationException("Unhandled result type.")
        };
    }

    /// <summary>
    /// Extracts the ResponseText from a completion response we already know to be
    /// successful.
    /// </summary>
    private static OuroResponseSuccess GetResponseText(CompletionCreateResponse response)
    {
        if (!response.Successful)
            throw new InvalidOperationException("Called GetResponseText on a response that was not marked successful. This should never happen.");

        var responseText = response
            .Choices
            .First()
            .Text
            .Trim();

        return new OuroResponseSuccess(responseText)
        {
            TotalTokenUsage = response.Usage.TotalTokens
        };
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

    public CompletionRequestHandler(OpenAIService api)
    {
        Api = api;
    }
}
