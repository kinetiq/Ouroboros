﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Betalgo.Ranul.OpenAI.Managers;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Betalgo.Ranul.OpenAI.ObjectModels.ResponseModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Extensions;
using Ouroboros.LargeLanguageModels.Resilience;
using Ouroboros.Responses;
using Ouroboros.StructuredOutput;
using Polly;

namespace Ouroboros.LargeLanguageModels.ChatCompletions;

internal class ChatRequestHandler : OpenAiRequestHandlerBase<ChatCompletionCreateResponse>
{
    private readonly ILogger<ChatRequestHandler> Logger;

    /// <summary>
    /// Executes a call to OpenAI using the ChatGPT API.
    /// </summary>
    public async Task<OuroResponseBase> CompleteAsync(List<ChatMessage> messages, OpenAIService api,
        ChatOptions? options = null)
    {
        options ??= new ChatOptions();

        // If a ResponseType is specified, we will use it to generate a schema for structured output.
        if (options.ResponseType != typeof(NoType))
            options.ResponseFormat = Json.GetSchema(options.ResponseType);

        // Map our generic options to OpenAI options.
        var request = ChatMappings.MapOptions(messages, options);

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
    /// Extracts details from a successful chat response.
    /// </summary>
    protected override OuroResponseBase HandlePolicySatisfied(ChatCompletionCreateResponse response, Type responseType)
    {
        // This happens when we hit an error that we don't want to bother retrying. Polly considers this
        // a success, but our OpenAI response will still show an error.
        if (!response.Successful)
            return new OuroResponseOpenAiError(response.Error);

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
    /// Get the ResultObject, if any. Otherwise, null.
    /// </summary>
    private static object? ResultObject(Type responseType, string responseText)
    {
        if(responseType == typeof(NoType)) 
            return null;
        
        return Json.ParseJson(responseText, responseType);
    }

    public ChatRequestHandler(ILogger<ChatRequestHandler>? logger)
    {
        Logger = logger ?? NullLogger<ChatRequestHandler>.Instance;
    }
}