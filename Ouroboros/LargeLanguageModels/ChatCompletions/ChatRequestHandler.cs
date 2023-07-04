using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenAI.Managers;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;
using Ouroboros.LargeLanguageModels.Completions;
using Ouroboros.LargeLanguageModels.Resilience;
using Polly;

namespace Ouroboros.LargeLanguageModels.ChatCompletions;
internal class ChatRequestHandler
{
    private readonly OpenAIService Api;

    /// <summary>
    /// Executes a call to OpenAI using the ChatGPT API.
    /// </summary>
    public async Task<CompleteResponseBase> CompleteAsync(List<ChatMessage> messages, ChatOptions? options = null)
    {
        options ??= new ChatOptions();

        // Map our generic options to OpenAI options.
        var request = ChatMappings.MapOptions(messages, options);

        var delay = BackoffPolicy.GetBackoffPolicy(options.UseExponentialBackOff);

        // TODO: consider more nuanced error handling: https://platform.openai.com/docs/guides/error-codes/api-errors

        var result = await Policy
            .Handle<Exception>()
            .OrResult<ChatCompletionCreateResponse>(x => x == null || !x.Successful)
            .WaitAndRetryAsync(delay)
            .ExecuteAndCaptureAsync(async () => await Api.ChatCompletion
                                                         .CreateCompletion(request));

        if (result.Outcome == OutcomeType.Successful)
            return GetResponseText(result.Result!);

        return result switch
        {
            { FaultType: FaultType.ExceptionHandledByThisPolicy } => new CompleteResponseFailure(result.FinalException!.Message),
            { FaultType: FaultType.ResultHandledByThisPolicy } => GetError(result.FinalHandledResult!),
            _ => throw new InvalidOperationException("Unhandled result type.")
        };
    }

    /// <summary>
    /// Extracts the ResponseText from from a completion response we already know to be
    /// successful.
    /// </summary>
    private static CompleteResponseSuccess GetResponseText(ChatCompletionCreateResponse response)
    {
        if (!response.Successful)
            throw new InvalidOperationException("Called GetResponseText on a response that was not marked successful. This should never happen.");

        var responseText = response.Choices
            .First()
            .Message
            .Content
            .Trim();

        return new CompleteResponseSuccess(responseText)
        {
            TotalTokenUsage = response.Usage.TotalTokens
        };
    }

    private static CompleteResponseFailure GetError(ChatCompletionCreateResponse completionResult)
    {
        if (completionResult.Successful)
            throw new InvalidOperationException("Called GetError on a response that was successful. This should never happen.");

        var error = completionResult.Error == null
            ? "Unknown Error"
            : $"{completionResult.Error.Code}: {completionResult.Error.Message}";

        return new CompleteResponseFailure(error);
    }

    public ChatRequestHandler(OpenAIService api)    
    {
        Api = api;
    }
}
