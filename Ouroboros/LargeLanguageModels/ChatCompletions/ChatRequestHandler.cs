using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenAI.Managers;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;
using Ouroboros.LargeLanguageModels.Resilience;
using Ouroboros.Responses;
using Polly;
using Z.Core.Extensions;

namespace Ouroboros.LargeLanguageModels.ChatCompletions;
internal class ChatRequestHandler
{
    private readonly OpenAIService Api;

    /// <summary>
    /// Executes a call to OpenAI using the ChatGPT API.
    /// </summary>
    public async Task<OuroResponseBase> CompleteAsync(List<ChatMessage> messages, ChatOptions? options = null)
    {
        options ??= new ChatOptions();

        // Map our generic options to OpenAI options.
        var request = ChatMappings.MapOptions(messages, options);

        var delay = BackoffPolicy.GetBackoffPolicy(options.UseExponentialBackOff);

        // TODO: consider more nuanced error handling: https://platform.openai.com/docs/guides/error-codes/api-errors

        // 401 should not retry.
        // 429 should retry.
        // 500 and 503 could retry. 

        var policyResult = await Policy
            .Handle<Exception>()
            .OrResult<ChatCompletionCreateResponse>(x => !x.Successful && (x.Error == null || x.Error.Code.In("429", "503")))
            .WaitAndRetryAsync(delay)
            .ExecuteAndCaptureAsync(async () => await Api.ChatCompletion
                                                         .CreateCompletion(request));

        if (policyResult.Outcome == OutcomeType.Successful)
        {
            var chat = policyResult.Result;

            if (chat == null)
                return new OuroResponseFailure("policyResult was successful, but the inner result was null. This should never happen.");

            if (!chat.Successful)
                return GetFailureResponse(chat);

            return GetSuccessResponse(chat);
        }

        return policyResult switch
        {
            { FaultType: FaultType.ExceptionHandledByThisPolicy } => new OuroResponseFailure(policyResult.FinalException!.Message),
            { FaultType: FaultType.ResultHandledByThisPolicy } => GetFailureResponse(policyResult.FinalHandledResult!),
            _ => throw new InvalidOperationException("Unhandled result type.")
        };
    }

    /// <summary>
    /// Extracts the ResponseText from from a completion response we already know to be
    /// successful.
    /// </summary>
    private static OuroResponseSuccess GetSuccessResponse(ChatCompletionCreateResponse response)
    {
        if (!response.Successful)
            throw new InvalidOperationException("Called GetResponseText on a response that was not marked successful. This should never happen.");

        var responseText = response.Choices
            .First()
            .Message
            .Content
            .Trim();

        return new OuroResponseSuccess(responseText)
        {
            PromptTokens = response.Usage.PromptTokens,
            CompletionTokens = response.Usage.CompletionTokens,
            TotalTokenUsage = response.Usage.TotalTokens
        };
    }

    private static OuroResponseFailure GetFailureResponse(ChatCompletionCreateResponse completionResult)
    {
        if (completionResult.Successful)
            throw new InvalidOperationException("Called GetError on a response that was successful. This should never happen.");

        var error = completionResult.Error == null
            ? "Unknown Error"
            : $"{completionResult.Error.Code}: {completionResult.Error.Message}";

        return new OuroResponseFailure(error);
    }

    public ChatRequestHandler(OpenAIService api)    
    {
        Api = api;
    }
}
