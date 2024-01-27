using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI.Managers;
using OpenAI.ObjectModels.ResponseModels;
using Ouroboros.LargeLanguageModels.Resilience;
using Ouroboros.Responses;
using Polly;
using System;
using System.Linq;
using System.Threading.Tasks;
using Z.Core.Extensions;

namespace Ouroboros.LargeLanguageModels.Completions;
internal class CompletionRequestHandler : OpenAiRequestHandlerBase<CompletionCreateResponse>
{
    private readonly ILogger<CompletionRequestHandler> Logger;

    public async Task<OuroResponseBase> CompleteAsync(string prompt, OpenAIService api, CompleteOptions? options)
    {
        options ??= new CompleteOptions();

        // Map our generic options to OpenAI options.
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
                    Logger.LogWarning("Delaying for {delay}ms, then attempting retry {retry}.", timespan.TotalMilliseconds, retryAttempt);
                })
            .ExecuteAndCaptureAsync(() => api.Completions
                .CreateCompletion(request));

        return base.HandleResponse(policyResult);
    }

    /// <summary>
    /// Extracts details from a successful completion response.
    /// </summary>
    protected override OuroResponseBase HandlePolicySatisfied(CompletionCreateResponse response)
    {
        // This happens when we hit an error that we don't want to bother retrying. Polly considers this
        // a success, but our OpenAI response will still show an error.
        if (!response.Successful)
            return new OuroResponseOpenAiError(response.Error);

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

    public CompletionRequestHandler(ILogger<CompletionRequestHandler>? logger)
    {
        Logger = logger ?? NullLogger<CompletionRequestHandler>.Instance;
    }
}
