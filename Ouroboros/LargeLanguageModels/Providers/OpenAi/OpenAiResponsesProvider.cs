using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
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

        return AttachmentRules.Validate(options, OuroProvider.OpenAi);
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

        // Where the last execution for each container landed, so files cited later can be attached
        // to it. See AttachGeneratedFiles for why the last one.
        var executionsByContainer = new Dictionary<string, int>();

        // Generated files are cited on the message text rather than on the execution that wrote
        // them, so they cannot be attached until every output item has been seen.
        var citations = new List<ContainerFileCitationMessageAnnotation>();

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

                        citations.AddRange(part.OutputTextAnnotations
                            .OfType<ContainerFileCitationMessageAnnotation>());
                    }

                    break;

                case CodeInterpreterCallResponseItem execution:
                    if (execution.ContainerId is { } containerId)
                        executionsByContainer[containerId] = blocks.Count;

                    blocks.Add(MapExecution(execution));
                    break;

                case ReasoningResponseItem:
                    break;

                default:
                    blocks.Add(new OuroUnknownBlock(item.Kind.ToString()));
                    break;
            }
        }

        AttachGeneratedFiles(blocks, executionsByContainer, citations);

        return blocks;
    }

    /// <summary>
    /// Maps one code interpreter call onto the neutral execution block.
    /// </summary>
    /// <remarks>
    /// Two things this API does not report, both derived here rather than left to look like data.
    ///
    /// There is no exit code. The status says whether the <em>tool</em> completed, so Completed maps
    /// to 0 and anything else to -1 with IsToolError set. A Python script that raises still counts
    /// as completed - the interpreter ran fine and the traceback is in the logs - which is exactly
    /// the distinction IsToolError already draws.
    ///
    /// There is no separate stderr either: the logs output is the combined stream, so Stderr stays
    /// empty rather than guessing which lines belonged to which.
    /// </remarks>
    private static OuroCodeExecutionBlock MapExecution(CodeInterpreterCallResponseItem execution)
    {
        var completed = execution.Status == CodeInterpreterCallStatus.Completed;

        var stdout = string.Concat(execution.Outputs
            .OfType<CodeInterpreterCallLogsOutput>()
            .Select(output => output.Logs));

        return new OuroCodeExecutionBlock
        {
            Code = execution.Code,
            Result = new OuroCodeExecutionResult
            {
                Stdout = stdout,
                ExitCode = completed ? 0 : -1,
                IsToolError = !completed
            }
        };
    }

    /// <summary>
    /// Attaches files the interpreter wrote to the execution they came from.
    /// </summary>
    /// <remarks>
    /// "Came from" is an approximation, deliberately. OpenAI cites a generated file against its
    /// <em>container</em> rather than against the call that wrote it, and one container serves every
    /// execution in the response - so when the model runs the tool twice, nothing on the wire says
    /// which run produced the chart.
    ///
    /// Attaching to the last execution in that container is the least-wrong reading: it is the most
    /// likely author, and it keeps each file appearing exactly once. Spreading them across every
    /// execution in the container would mean a caller enumerating CodeExecutions for artifacts
    /// downloads the same file repeatedly.
    /// </remarks>
    private static void AttachGeneratedFiles(List<OuroContentBlock> blocks,
        Dictionary<string, int> executionsByContainer,
        List<ContainerFileCitationMessageAnnotation> citations)
    {
        if (citations.Count == 0)
            return;

        var filesByExecution = new Dictionary<int, List<OuroFileRef>>();

        foreach (var citation in citations)
        {
            if (citation.ContainerId is null
                || !executionsByContainer.TryGetValue(citation.ContainerId, out var index))
            {
                continue;
            }

            if (!filesByExecution.TryGetValue(index, out var files))
                filesByExecution[index] = files = [];

            // Deduplicated by id. A file the prose mentions twice is cited twice, and handing the
            // caller the same artifact twice would be an artefact of the mapper, not a fact.
            if (files.Exists(file => file.Id == citation.FileId))
                continue;

            files.Add(new OuroFileRef(citation.FileId)
            {
                Provider = OuroProvider.OpenAi,
                FileName = citation.Filename,

                // Without this the id is unusable: container files live behind a different endpoint
                // from the account-wide store, and fetching one needs both ids.
                ContainerScope = citation.ContainerId
            });
        }

        foreach (var (index, files) in filesByExecution)
        {
            var execution = (OuroCodeExecutionBlock)blocks[index];

            blocks[index] = execution with
            {
                Result = (execution.Result ?? new OuroCodeExecutionResult()) with { Files = files }
            };
        }
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
