using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI.Managers;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;
using Ouroboros.LargeLanguageModels.Resilience;
using Ouroboros.Responses;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Z.Core.Extensions;

namespace Ouroboros.LargeLanguageModels.ChatCompletions;
internal class ChatRequestHandler
{
    private readonly IServiceProvider Services;

    /// <summary>
    /// Executes a call to OpenAI using the ChatGPT API.
    /// </summary>
    public async Task<OuroResponseBase> CompleteAsync(List<ChatMessage> messages, OpenAIService api, ChatOptions? options = null)
    {
        options ??= new ChatOptions();

        // Map our generic options to OpenAI options.
        var request = ChatMappings.MapOptions(messages, options);

        var delay = BackoffPolicy.GetBackoffPolicy(options.UseExponentialBackOff);

        // OpenAI errors: https://platform.openai.com/docs/guides/error-codes/api-errors

        var policyResult = await Policy
            .Handle<Exception>()
            .OrResult<ChatCompletionCreateResponse>(response => 
                !response.Successful && 
                (response.Error == null || response.Error.Code.In("429", "500", "503")))
            .WaitAndRetryAsync(
                sleepDurations: delay,
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    var logger = Services.GetService<ILogger<ChatRequestHandler>>();

                    if (logger == null)
                        return;

                    // TODO: look into outcome and try to log more information

                    logger?.LogWarning("Delaying for {delay}ms, then attempting retry {retry}.", timespan.TotalMilliseconds, retryAttempt);
                })
            .ExecuteAndCaptureAsync(async () => await api.ChatCompletion
                                                         .CreateCompletion(request));

        if (policyResult.Outcome == OutcomeType.Successful)
        {
            var chat = policyResult.Result;

            if (chat == null)
                return new OuroResponseInternalError("While retrying, PolicyResult was successful, however the inner result was null. This should never happen.");

            if (!chat.Successful) // the response can still be a failure at this point
                return new OuroResponseOpenAiError(chat.Error);

            return GetSuccessResponse(chat);
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
    private static OuroResponseSuccess GetSuccessResponse(ChatCompletionCreateResponse response)
    {
        if (!response.Successful)
            throw new InvalidOperationException("Called GetResponseText on a response that was not marked successful. This should never happen.");

        var responseText = response.Choices
            .First()
            .Message
            .Content!
            .Trim();

        return new OuroResponseSuccess(responseText)
        {
            Model = response.Model,
            PromptTokens = response.Usage.PromptTokens,
            CompletionTokens = response.Usage.CompletionTokens,
            TotalTokenUsage = response.Usage.TotalTokens
        };
    }

    public ChatRequestHandler(IServiceProvider services)
    {
        Services = services;
    }
}
