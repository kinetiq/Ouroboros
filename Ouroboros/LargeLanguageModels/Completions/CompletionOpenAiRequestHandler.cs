using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Betalgo.Ranul.OpenAI.Managers;
using Betalgo.Ranul.OpenAI.ObjectModels.ResponseModels;
using Ouroboros.Extensions;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.LargeLanguageModels.Resilience;
using Ouroboros.Responses;
using Ouroboros.StructuredOutput;
using Polly;

namespace Ouroboros.LargeLanguageModels.Completions;

internal class CompletionRequestHandler(ILogger<CompletionRequestHandler>? logger)
    : OpenAiRequestHandlerBase<CompletionCreateResponse>
{
    private readonly ILogger<CompletionRequestHandler> Logger = logger ?? NullLogger<CompletionRequestHandler>.Instance;

    public async Task<OuroResponseBase> CompleteAsync(string prompt, OpenAIService api, CompleteOptions? options)
    {
        options ??= new CompleteOptions();

        // Map our generic options to OpenAI options.
        var request = CompletionMappings.MapOptions(prompt, options);
        var delay = BackoffPolicy.GetBackoffPolicy(options.UseExponentialBackOff);

        // OpenAI errors: https://platform.openai.com/docs/guides/error-codes/api-errors

        Logger.LogInformation(
            "Sending complete message (length {size}) to OpenAI with UseExponentialBackoff = {useBackoff}",
            prompt.Length, options.UseExponentialBackOff);

        var policyResult = await Policy
            .Handle<Exception>()
            .OrResult<CompletionCreateResponse>(response =>
                !response.Successful &&
                (response.Error == null || response.Error.Code.In("429", "500", "503")))
            .WaitAndRetryAsync(
                delay,
                (outcome, timespan, retryAttempt, context) =>
                {
                    Logger.LogWarning("Delaying for {delay}ms, then attempting retry {retry}.",
                        timespan.TotalMilliseconds, retryAttempt);
                })
            .ExecuteAndCaptureAsync(() => api.Completions
                .CreateCompletion(request));

        return HandleResponse(policyResult, typeof(NoType));
    }

    /// <summary>
    /// Extracts details from a successful completion response. Note that Completion does not use responseType,
    /// that's for Chat.
    /// </summary>
    protected override OuroResponseBase HandlePolicySatisfied(CompletionCreateResponse response, Type responseType)
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
            ResponseObject = null,
            PromptTokens = response.Usage.PromptTokens,
            CompletionTokens = response.Usage.CompletionTokens,
            TotalTokenUsage = response.Usage.TotalTokens
        };
    }
}