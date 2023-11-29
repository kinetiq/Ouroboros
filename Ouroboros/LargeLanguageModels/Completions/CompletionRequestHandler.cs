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

        var policyResult = await Policy
            .Handle<Exception>()
            .OrResult<CompletionCreateResponse>(x => x == null || !x.Successful)
            .WaitAndRetryAsync(delay)
            .ExecuteAndCaptureAsync(async () => await Api.Completions.CreateCompletion(request));

        if (policyResult.Outcome == OutcomeType.Successful)
        {
            var completion = policyResult.Result;

            if (completion == null)
                return new OuroResponseFailure("policyResult was successful, however the inner result was null. This should never happen.");

            if (!completion.Successful) // the response can still be a failure at this point
                return GetFailureResponse(completion);

            return GetSuccessResponse(completion);
        }

        return policyResult switch
        {
            { FaultType: FaultType.ExceptionHandledByThisPolicy } => new OuroResponseFailure(policyResult.FinalException!.Message),
            { FaultType: FaultType.ResultHandledByThisPolicy } => GetFailureResponse(policyResult.FinalHandledResult!),
            _ => throw new InvalidOperationException("Unhandled result type.")
        };
    }

    /// <summary>
    /// Extracts the ResponseText from a completion response we already know to be
    /// successful.
    /// </summary>
    private static OuroResponseSuccess GetSuccessResponse(CompletionCreateResponse response)
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
            PromptTokens = response.Usage.PromptTokens,
            CompletionTokens = response.Usage.CompletionTokens,
            TotalTokenUsage = response.Usage.TotalTokens
        };
    }

    private static OuroResponseFailure GetFailureResponse(CompletionCreateResponse completionResult)
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
