using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI.Responses;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.Responses;
using Ouroboros.StructuredOutput;

namespace Ouroboros.LargeLanguageModels.Providers.OpenAi;

/// <summary>
/// OpenAI, over the Responses API.
/// </summary>
/// <remarks>
/// One attempt per call. Retry, timeout and cancellation are ChatExecutor's job - see IChatProvider.
///
/// This replaced a Chat Completions provider built on a third-party SDK. Responses is where
/// OpenAI's server-side tools live, so it is the only surface on which this provider can reach
/// parity with the Anthropic one.
/// </remarks>
internal sealed class OpenAiResponsesProvider(ResponsesClient client, ILogger? logger = null) : IChatProvider
{
    private readonly ILogger Logger = logger ?? NullLogger.Instance;

    public OuroProvider Kind => OuroProvider.OpenAi;

    public async Task<ProviderAttempt> SendAsync(List<OuroMessage> messages, ChatOptions options,
        CancellationToken cancellationToken)
    {
        if (Reject(options) is { } refusal)
            return ProviderAttempt.Final(refusal);

        var request = OpenAiMappings.MapOptions(messages, options);

        try
        {
            ResponseResult response = await client.CreateResponseAsync(request, cancellationToken);

            return MapResult(response, options.ResponseType);
        }
        catch (ClientResultException ex)
        {
            var error = new OuroResponseProviderError("OpenAI", ex.Status.ToString(), ex.Message);

            // Rate limiting and server faults are worth another attempt; a 4xx about the request
            // itself is not - retrying just repeats it.
            var retryable = ex.Status == 429 || ex.Status >= 500;

            return new ProviderAttempt(error, retryable);
        }
    }

    /// <summary>
    /// Catches request shapes this API cannot honour, before spending a call.
    /// </summary>
    private static OuroResponseBase? Reject(ChatOptions options)
    {
        // The Responses API has no stop parameter at all - this is an API-level gap, not an SDK
        // omission. Silently dropping the sequences would produce output that runs past where the
        // caller asked it to stop, which is worse than refusing.
        if (options.StopSequences is { Count: > 0 })
            return new OuroResponseInternalError(
                "ChatOptions.StopSequences is not supported on OpenAI's Responses API, which has no "
                + "stop parameter. Remove them, or route the call to a Claude model.");

        if (options.ServerTools != OuroServerTools.None)
            return new OuroResponseInternalError(
                $"{options.ServerTools} is not wired up for OpenAI yet. Route this call to a Claude "
                + "model, which serves it today.");

        if (options.Attachments is { Count: > 0 })
            return new OuroResponseInternalError(
                "Attachments are not wired up for OpenAI yet. Route this call to a Claude model.");

        return null;
    }

    private static ProviderAttempt MapResult(ResponseResult response, Type? responseType)
    {
        // A failed status carries the error in-band rather than throwing.
        if (response.Status == ResponseStatus.Failed && response.Error is { } error)
        {
            var failure = new OuroResponseProviderError("OpenAI", error.Code.ToString(), error.Message);

            return new ProviderAttempt(failure, error.Code == ResponseErrorCode.RateLimitExceeded);
        }

        var blocks = MapContent(response);

        // GetOutputText concatenates the output_text parts, which on a structured call is the raw
        // JSON. Consumers persist that verbatim as their durable record, so it must stay the text.
        var responseText = response.GetOutputText()?.Trim() ?? "";
        var usage = response.Usage;

        var success = new OuroResponseSuccess(blocks)
        {
            Model = response.Model ?? "",
            ResponseObject = ResponseParser.Parse(responseType, responseText),
            StopReason = MapStopReason(response),
            PromptTokens = usage?.InputTokenCount ?? 0,
            CompletionTokens = usage?.OutputTokenCount,
            TotalTokenUsage = usage?.TotalTokenCount ?? 0
        };

        return ProviderAttempt.Final(success);
    }

    /// <summary>
    /// Turns the response's output items into Ouroboros blocks.
    /// </summary>
    /// <remarks>
    /// Reasoning items are skipped for the same reason Anthropic's thinking blocks are: the raw
    /// chain of thought is not returned, so there is nothing in them worth a block. Anything not
    /// modelled becomes an unknown block rather than being dropped.
    /// </remarks>
    private static List<OuroContentBlock> MapContent(ResponseResult response)
    {
        var blocks = new List<OuroContentBlock>();

        foreach (var item in response.OutputItems)
        {
            switch (item)
            {
                case MessageResponseItem message:
                    foreach (var part in message.Content)
                    {
                        // Trimmed here because OuroResponseSuccess trims the joined text it derives
                        // from these blocks. Leaving the block untrimmed makes a caller reading
                        // block.Text and a caller reading ResponseText disagree about whitespace.
                        var text = part.Text?.Trim();

                        if (!string.IsNullOrEmpty(text))
                            blocks.Add(new OuroTextBlock(text));
                    }

                    break;

                case ReasoningResponseItem:
                    break;

                default:
                    blocks.Add(new OuroUnknownBlock(item.Kind.ToString()));
                    break;
            }
        }

        return blocks;
    }

    /// <summary>
    /// Maps the response status onto the provider-neutral stop reason.
    /// </summary>
    /// <remarks>
    /// Truncation is reported as an Incomplete status with a reason, rather than as a distinct
    /// finish reason. Getting this wrong makes a cut-off response indistinguishable from a
    /// complete one - which is exactly the bug the stop reason exists to expose.
    /// </remarks>
    private static OuroStopReason MapStopReason(ResponseResult response)
    {
        if (response.Status == ResponseStatus.Incomplete)
        {
            return response.IncompleteStatusDetails?.Reason == ResponseIncompleteStatusReason.MaxOutputTokens
                ? OuroStopReason.MaxTokens
                : OuroStopReason.Unknown;
        }

        return response.Status == ResponseStatus.Completed
            ? OuroStopReason.EndTurn
            : OuroStopReason.Unknown;
    }
}
