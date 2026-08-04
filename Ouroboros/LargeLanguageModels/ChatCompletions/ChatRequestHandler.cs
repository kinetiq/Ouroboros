using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Betalgo.Ranul.OpenAI.Managers;
using Betalgo.Ranul.OpenAI.ObjectModels.ResponseModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Core;
using Ouroboros.Extensions;
using Ouroboros.LargeLanguageModels.Resilience;
using Ouroboros.Responses;
using Ouroboros.StructuredOutput;
using Polly;

namespace Ouroboros.LargeLanguageModels.ChatCompletions;

internal class ChatRequestHandler
{
    private readonly ILogger<ChatRequestHandler> Logger;

    /// <summary>
    /// Executes a call to OpenAI using the ChatGPT API.
    /// </summary>
    /// <remarks>
    /// Virtual so tests can substitute a canned response and exercise OuroClient without a network
    /// call. Nothing in the library overrides it.
    /// </remarks>
    public virtual async Task<OuroResponseBase> CompleteAsync(List<OuroMessage> messages, OpenAIService api,
        ChatOptions? options = null)
    {
        options ??= new ChatOptions();

        // Map our generic options to OpenAI options. The structured-output schema is built there
        // from options.ResponseType.
        var request = ChatMappings.MapOptions(messages, options, Logger);

        var delay = BackoffPolicy.GetBackoffPolicy(options.UseExponentialBackOff);

        // OpenAI errors: https://platform.openai.com/docs/guides/error-codes/api-errors

        Logger.LogInformation(
            "Sending {count} chat messages to OpenAI with UseExponentialBackoff = {useBackoff}", messages.Count,
            options.UseExponentialBackOff);

        var policyResult = await Policy
            .Handle<Exception>()
            .OrResult<ChatCompletionCreateResponse>(response =>
                !response.Successful &&
                (response.Error == null || response.Error.Code.In("429", "500", "503")))
            .WaitAndRetryAsync(
                delay,
                (outcome, timespan, retryAttempt, context) =>
                {
                    Logger.LogWarning("Delaying for {delay}ms, then attempting retry {retry}.",
                        timespan.TotalMilliseconds, retryAttempt);
                })
            .ExecuteAndCaptureAsync(() => api.ChatCompletion
                .CreateCompletion(request)
            );

        return HandleResponse(policyResult, options.ResponseType);
    }

    /// <summary>
    /// Unwraps the Polly result into an Ouroboros response.
    /// </summary>
    private OuroResponseBase HandleResponse(PolicyResult<ChatCompletionCreateResponse> policyResult, Type? responseType)
    {
        if (policyResult.Outcome == OutcomeType.Successful)
        {
            var response = policyResult.Result;

            if (response == null)
                return new OuroResponseInternalError("PolicyResult was successful, however the inner result was null. This should never happen.");

            return HandlePolicySatisfied(response, responseType);
        }

        return HandlePolicyExhausted(policyResult);
    }

    /// <summary>
    /// Extracts details from a successful chat response.
    /// </summary>
    private static OuroResponseBase HandlePolicySatisfied(ChatCompletionCreateResponse response, Type? responseType)
    {
        // This happens when we hit an error that we don't want to bother retrying. Polly considers this
        // a success, but our OpenAI response will still show an error.
        if (!response.Successful)
            return new OuroResponseProviderError("OpenAI", response.Error?.Code, response.Error?.Message);

        var responseText = response.Choices
            .First()
            .Message
            .Content!
            .Trim();

        return new OuroResponseSuccess(responseText)
        {
            Model = response.Model,
            ResponseObject = ResultObject(responseType, responseText),
            PromptTokens = response.Usage.PromptTokens,
            CompletionTokens = response.Usage.CompletionTokens,
            TotalTokenUsage = response.Usage.TotalTokens
        };
    }

    /// <summary>
    /// Called when the retry policy gave up. Distinguishes a thrown exception from a
    /// returned-but-unsuccessful response.
    /// </summary>
    private static OuroResponseFailure HandlePolicyExhausted(PolicyResult<ChatCompletionCreateResponse> policyResult)
    {
        return policyResult switch
        {
            { FaultType: FaultType.ExceptionHandledByThisPolicy } =>
                new OuroResponseInternalError("Exception calling endpoint: " + policyResult.FinalException!.Message),
            { FaultType: FaultType.ResultHandledByThisPolicy } =>
                new OuroResponseProviderError(
                    "OpenAI",
                    policyResult.FinalHandledResult!.Error?.Code,
                    policyResult.FinalHandledResult!.Error?.Message),
            _ => throw new InvalidOperationException("Unhandled result type.")
        };
    }

    /// <summary>
    /// Get the ResultObject, if any. Otherwise, null.
    /// Handles reasoning models that concatenate reasoning output + structured JSON.
    /// </summary>
    private static object? ResultObject(Type? responseType, string responseText)
    {
        if (responseType is null)
            return null;

        try
        {
            return Json.ParseJson(responseText, responseType);
        }
        catch (JsonException)
        {
            // Reasoning models may return multiple JSON blocks separated by newlines.
            // The structured output is typically the last block.
            var lastIndex = responseText.LastIndexOf("\n{");
            if (lastIndex >= 0)
            {
                var lastBlock = responseText[(lastIndex + 1)..];
                try { return Json.ParseJson(lastBlock, responseType); }
                catch { /* fall through */ }
            }

            return null;
        }
    }

    public ChatRequestHandler(ILogger<ChatRequestHandler>? logger)
    {
        Logger = logger ?? NullLogger<ChatRequestHandler>.Instance;
    }
}
