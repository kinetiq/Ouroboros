using OpenAI.Managers;
using OpenAI.ObjectModels.ResponseModels;
using Ouroboros.LargeLanguageModels.Resilience;
using Ouroboros.Responses;
using Polly;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Z.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Ouroboros.LargeLanguageModels.Completions;
internal class CompletionRequestHandler
{
    private readonly IServiceProvider Services;

    public async Task<OuroResponseBase> Complete(string prompt, OpenAIService api, CompleteOptions? options)
    {
        options ??= new CompleteOptions();

        var request = Mappings.MapOptions(prompt, options);

        var delay = BackoffPolicy.GetBackoffPolicy(options.UseExponentialBackOff);

        // OpenAI errors: https://platform.openai.com/docs/guides/error-codes/api-errors

        var policyResult = await Policy
            .Handle<Exception>()
            .OrResult<CompletionCreateResponse>(response =>
                !response.Successful &&
                (response.Error == null || response.Error.Code.In("429", "500", "503")))
            .WaitAndRetryAsync(
                sleepDurations: delay,
                onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                var logger = Services.GetService<ILogger<CompletionRequestHandler>>();

                if (logger == null)
                    return;

                // TODO: look into outcome and try to log more information

                logger?.LogWarning("Delaying for {delay}ms, then attempting retry {retry}.", timespan.TotalMilliseconds, retryAttempt);
            })
            .ExecuteAndCaptureAsync(async () => await api.Completions
                                                         .CreateCompletion(request));


        if (policyResult.Outcome == OutcomeType.Successful)
        {
            var completion = policyResult.Result;

            if (completion == null)
                return new OuroResponseFailure("policyResult was successful, however the inner result was null. This should never happen.");

            if (!completion.Successful) // the response can still be a failure at this point
                return new OuroResponseOpenAiError(completion.Error);

            return GetSuccessResponse(completion);
        }

        return policyResult switch
        {
            { FaultType: FaultType.ExceptionHandledByThisPolicy } => new OuroResponseFailure(policyResult.FinalException!.Message),
            { FaultType: FaultType.ResultHandledByThisPolicy } => new OuroResponseOpenAiError(policyResult.FinalHandledResult!.Error),
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
            Model = response.Model,
            PromptTokens = response.Usage.PromptTokens,
            CompletionTokens = response.Usage.CompletionTokens,
            TotalTokenUsage = response.Usage.TotalTokens
        };
    }

    public CompletionRequestHandler(IServiceProvider services)
    {
        this.Services = services;
    }
}
