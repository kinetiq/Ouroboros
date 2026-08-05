using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Betalgo.Ranul.OpenAI.Managers;
using Betalgo.Ranul.OpenAI.ObjectModels.ResponseModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Core;
using Ouroboros.Extensions;
using Ouroboros.LargeLanguageModels.Providers;
using Ouroboros.Responses;
using Ouroboros.StructuredOutput;

namespace Ouroboros.LargeLanguageModels.ChatCompletions;

/// <summary>
/// OpenAI, over the Chat Completions API.
/// </summary>
/// <remarks>
/// One attempt per call. Retry, timeout and cancellation are ChatExecutor's job - see IChatProvider.
///
/// Chat Completions has no server-side tools, so this provider can never satisfy a request for code
/// execution or MCP. It says so explicitly rather than dropping the flag, which would leave a caller
/// wondering why the model politely explained it could not run code.
/// </remarks>
internal sealed class OpenAiChatProvider(OpenAIService api, ILogger? logger = null) : IChatProvider
{
    private readonly ILogger Logger = logger ?? NullLogger.Instance;

    public OuroProvider Kind => OuroProvider.OpenAi;

    public async Task<ProviderAttempt> SendAsync(List<OuroMessage> messages, ChatOptions options,
        CancellationToken cancellationToken)
    {
        // Fail rather than drop the flag. Silently ignoring it produces a response in which the
        // model explains it is unable to run code, which reads as a model limitation rather than a
        // configuration mistake and is miserable to diagnose.
        if (options.ServerTools != OuroServerTools.None)
            return ProviderAttempt.Final(new OuroResponseInternalError(
                $"{options.ServerTools} was requested, but OpenAI's Chat Completions API has no " +
                "server-side tools. Route this call to a Claude model, which serves them on the " +
                "standard Messages API."));

        // Map our generic options to OpenAI options. The structured-output schema is built there
        // from options.ResponseType.
        var request = ChatMappings.MapOptions(messages, options, Logger);

        // OpenAI errors: https://platform.openai.com/docs/guides/error-codes/api-errors
        var response = await api.ChatCompletion.CreateCompletion(request, cancellationToken: cancellationToken);

        if (!response.Successful)
        {
            var error = new OuroResponseProviderError("OpenAI", response.Error?.Code, response.Error?.Message);

            // No error object at all means we cannot tell what went wrong, so give it another go
            // rather than surfacing a failure we have no explanation for.
            var retryable = response.Error == null || response.Error.Code.In("429", "500", "503");

            return new ProviderAttempt(error, retryable);
        }

        return ProviderAttempt.Final(MapSuccess(response, options.ResponseType));
    }

    /// <summary>
    /// Turns a successful provider response into an Ouroboros one.
    /// </summary>
    private static OuroResponseBase MapSuccess(ChatCompletionCreateResponse response, Type? responseType)
    {
        // Previously Choices.First().Message.Content!.Trim() - which throws on an empty choice list
        // and null-forgives a field that is genuinely null whenever the turn carried no text.
        var choice = response.Choices?.FirstOrDefault();

        if (choice?.Message?.Content is not { } content)
            return new OuroResponseInternalError(
                "The provider reported success but returned no message content.");

        var responseText = content.Trim();

        // Usage is absent on some responses and was previously dereferenced without a check.
        var usage = response.Usage;

        // Chat Completions is single-block by nature, so this is always exactly one text block.
        // Providers with server-side tools contribute more, which is the point of the model.
        return new OuroResponseSuccess([new OuroTextBlock(responseText)])
        {
            Model = response.Model,
            ResponseObject = ResultObject(responseType, responseText),
            StopReason = MapStopReason(choice.FinishReason),
            PromptTokens = usage?.PromptTokens ?? 0,
            CompletionTokens = usage?.CompletionTokens,
            TotalTokenUsage = usage?.TotalTokens ?? 0
        };
    }

    /// <summary>
    /// Maps OpenAI's finish_reason onto the provider-neutral enum.
    /// </summary>
    /// <remarks>
    /// "stop" covers both a natural ending and a stop sequence on this API - they are not
    /// distinguishable here, so both map to EndTurn. Providers that do distinguish them report
    /// StopSequence.
    /// </remarks>
    private static OuroStopReason MapStopReason(string? finishReason)
    {
        return finishReason switch
        {
            "stop" => OuroStopReason.EndTurn,
            "length" => OuroStopReason.MaxTokens,
            _ => OuroStopReason.Unknown
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
            var lastIndex = responseText.LastIndexOf("\n{", StringComparison.Ordinal);
            if (lastIndex >= 0)
            {
                var lastBlock = responseText[(lastIndex + 1)..];
                try { return Json.ParseJson(lastBlock, responseType); }
                catch { /* fall through */ }
            }

            return null;
        }
    }
}
