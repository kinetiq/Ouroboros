using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AnthropicSdk = Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.Responses;

namespace Ouroboros.LargeLanguageModels.Providers.Anthropic;

/// <summary>
/// Anthropic, over the Messages API.
/// </summary>
/// <remarks>
/// One attempt per call. Retry, timeout and cancellation are ChatExecutor's job - see IChatProvider.
///
/// Unlike OpenAI's Chat Completions, this API carries server-side tools natively, which is why code
/// execution and MCP land here first: they are a tool declaration on the request rather than a
/// different API surface.
/// </remarks>
internal sealed class AnthropicChatProvider(AnthropicSdk.AnthropicClient client, ILogger? logger = null) : IChatProvider
{
    private readonly ILogger Logger = logger ?? NullLogger.Instance;

    public OuroProvider Kind => OuroProvider.Anthropic;

    public async Task<ProviderAttempt> SendAsync(List<OuroMessage> messages, ChatOptions options,
        CancellationToken cancellationToken)
    {
        if (Reject(options) is { } refusal)
            return ProviderAttempt.Final(refusal);

        var parameters = AnthropicMappings.MapOptions(messages, options);

        try
        {
            var message = await client.Messages.Create(parameters, cancellationToken);

            return ProviderAttempt.Final(MapSuccess(message));
        }
        catch (AnthropicSdk.Exceptions.AnthropicRateLimitException ex)
        {
            return ProviderAttempt.Retryable(Error("429", ex.Message));
        }
        catch (AnthropicSdk.Exceptions.Anthropic5xxException ex)
        {
            return ProviderAttempt.Retryable(Error("5xx", ex.Message));
        }
        catch (AnthropicSdk.Exceptions.AnthropicApiException ex)
        {
            // Everything else the API reports is the request's own fault - a bad model id, a
            // rejected parameter, an auth problem. Retrying just repeats it.
            return ProviderAttempt.Final(Error(null, ex.Message));
        }
    }

    /// <summary>
    /// Catches request shapes the provider would accept but not honour, before spending a call.
    /// </summary>
    /// <remarks>
    /// Attachments ride a container_upload block, which only means something inside a code
    /// execution container. Sent without one they are accepted and ignored, and the model answers
    /// as though the file were never mentioned - which reads as the model being obtuse rather than
    /// the request being wrong.
    /// </remarks>
    private static OuroResponseBase? Reject(ChatOptions options)
    {
        // Structured output is not wired up on this provider yet. Anthropic supports it, but
        // generating the schema means untangling StructuredOutput.Json from the Betalgo types it
        // still returns - work that belongs with the move off that SDK.
        //
        // Until then this has to fail. Ignoring it would send the request unconstrained, the
        // response would parse to null, and the caller would get ResponseObject == null on an
        // otherwise successful call - indistinguishable from a model that returned nothing useful.
        if (options.ResponseType is not null)
            return new OuroResponseInternalError(
                $"ChatOptions.ResponseType ({options.ResponseType.Name}) is not supported on "
                + "Anthropic yet - structured output is only implemented for OpenAI. Route calls "
                + "that need it to a GPT model.");

        if (options.Attachments is not { Count: > 0 } attachments)
            return null;

        if (!options.ServerTools.HasFlag(OuroServerTools.CodeExecution))
            return new OuroResponseInternalError(
                "Attachments were supplied without OuroServerTools.CodeExecution. Files are mounted "
                + "into the execution container, so without one there is nowhere for them to go.");

        foreach (var attachment in attachments)
        {
            if (attachment.Provider != OuroProvider.Anthropic)
                return new OuroResponseInternalError(
                    $"Attachment '{attachment.Id}' belongs to {attachment.Provider}, but this call "
                    + "routes to Anthropic. File references are not portable between providers - "
                    + "upload the file to the one serving the call.");
        }

        return null;
    }

    private static OuroResponseProviderError Error(string? code, string message)
    {
        return new OuroResponseProviderError("Anthropic", code, message);
    }

    private static OuroResponseBase MapSuccess(Message message)
    {
        var blocks = MapContent(message.Content);

        var usage = message.Usage;

        return new OuroResponseSuccess(blocks)
        {
            // Cast, never ToString(). These SDK wrappers serialise themselves as JSON, so ToString()
            // yields "claude-opus-5" complete with the quotation marks - which would be stored
            // verbatim here and would silently never match in MapStopReason below.
            Model = (string?)message.Model ?? "",
            StopReason = MapStopReason(message.StopReason is { } stop ? (string?)stop : null),
            PromptTokens = (int)(usage?.InputTokens ?? 0),
            CompletionTokens = (int?)usage?.OutputTokens,
            TotalTokenUsage = (int)((usage?.InputTokens ?? 0) + (usage?.OutputTokens ?? 0))
        };
    }

    /// <summary>
    /// Turns Anthropic's content array into Ouroboros blocks.
    /// </summary>
    /// <remarks>
    /// Code execution arrives as two separate blocks correlated by id - a server_tool_use carrying
    /// the code, then a result block carrying stdout. They are coalesced into one
    /// OuroCodeExecutionBlock here, so the join lives in the mapper rather than in every consumer.
    ///
    /// Anything not yet modelled becomes an OuroUnknownBlock rather than being dropped, so a new
    /// provider block type degrades visibly instead of quietly shortening the response.
    /// </remarks>
    private static List<OuroContentBlock> MapContent(IReadOnlyList<ContentBlock>? content)
    {
        var blocks = new List<OuroContentBlock>();

        if (content is null)
            return blocks;

        // Where each pending execution landed, so its result can be filled in when it arrives.
        var executionsByToolUseId = new Dictionary<string, int>();

        foreach (var block in content)
        {
            if (block.TryPickText(out TextBlock? text))
            {
                blocks.Add(new OuroTextBlock(text!.Text));
                continue;
            }

            // Thinking blocks are deliberately not surfaced: the raw chain of thought is never
            // returned by current models, so there is nothing in them worth a block.
            if (block.TryPickThinking(out _) || block.TryPickRedactedThinking(out _))
                continue;

            if (block.TryPickServerToolUse(out ServerToolUseBlock? toolUse) && toolUse is not null)
            {
                executionsByToolUseId[toolUse.ID] = blocks.Count;
                blocks.Add(new OuroCodeExecutionBlock { Code = ExtractCode(toolUse) });
                continue;
            }

            if (block.TryPickBashCodeExecutionToolResult(out BashCodeExecutionToolResultBlock? result)
                && result is not null)
            {
                Attach(blocks, executionsByToolUseId, result.ToolUseID, MapExecutionResult(result));
                continue;
            }

            // The code execution tool has a file-editor half as well as a shell half - asked for
            // anything non-trivial the model writes a script with `create` and then runs it with
            // bash, producing two invocations and two different result shapes. Handling only the
            // bash one leaves the create invocation orphaned with a null Result.
            if (block.TryPickTextEditorCodeExecutionToolResult(out TextEditorCodeExecutionToolResultBlock? edit)
                && edit is not null)
            {
                Attach(blocks, executionsByToolUseId, edit.ToolUseID, MapEditResult(edit));
                continue;
            }

            blocks.Add(new OuroUnknownBlock(DescribeBlock(block)));
        }

        return blocks;
    }

    /// <summary>
    /// Fills in the result on the execution block its tool-use id points at.
    /// </summary>
    /// <remarks>
    /// A result with no matching invocation becomes its own block rather than being discarded -
    /// dropping it would lose the only record that something ran.
    /// </remarks>
    private static void Attach(List<OuroContentBlock> blocks, Dictionary<string, int> executionsByToolUseId,
        string? toolUseId, OuroCodeExecutionResult mapped)
    {
        if (toolUseId is { } id
            && executionsByToolUseId.TryGetValue(id, out var index)
            && blocks[index] is OuroCodeExecutionBlock pending)
        {
            blocks[index] = pending with { Result = mapped };
            return;
        }

        blocks.Add(new OuroCodeExecutionBlock { Result = mapped });
    }

    /// <summary>
    /// Maps a file-editor result onto the same shape as a shell one.
    /// </summary>
    /// <remarks>
    /// These carry no stdout or exit code - the useful signal is just whether the edit worked - so
    /// they are normalised into the common shape rather than given a block type of their own. A
    /// caller iterating CodeExecutions sees the whole sequence the model ran, in order.
    /// </remarks>
    private static OuroCodeExecutionResult MapEditResult(TextEditorCodeExecutionToolResultBlock edit)
    {
        if (edit.Content is not { } content)
            return new OuroCodeExecutionResult();

        if (content.TryPickTextEditorCodeExecutionToolResultError(out var error) && error is not null)
        {
            return new OuroCodeExecutionResult
            {
                IsToolError = true,
                Stderr = (string?)error.ErrorCode ?? "The file editor reported an error.",
                ExitCode = -1
            };
        }

        // Viewing a file is the one editor operation with output worth keeping.
        if (content.TryPickTextEditorCodeExecutionViewResultBlock(out var view) && view is not null)
            return new OuroCodeExecutionResult { Stdout = view.Content ?? "" };

        return new OuroCodeExecutionResult();
    }

    private static OuroCodeExecutionResult MapExecutionResult(BashCodeExecutionToolResultBlock result)
    {
        if (result.Content is { } content
            && content.TryPickBashCodeExecutionResultBlock(out var success)
            && success is not null)
        {
            return new OuroCodeExecutionResult
            {
                Stdout = success.Stdout ?? "",
                Stderr = success.Stderr ?? "",
                ExitCode = (int)success.ReturnCode,
                Files = MapOutputFiles(success.Content)
            };
        }

        // The tool itself failed - timed out, ran out of memory, was unavailable - rather than the
        // code running and exiting non-zero. Worth distinguishing: one is worth retrying, the other
        // is the model's problem to fix.
        var errorCode = result.Content is { } errorContent
                        && errorContent.TryPickBashCodeExecutionToolResultError(out var error)
                        && error is not null
            ? (string?)error.ErrorCode
            : null;

        return new OuroCodeExecutionResult
        {
            IsToolError = true,
            Stderr = errorCode ?? "The code execution tool reported an error.",
            ExitCode = -1
        };
    }

    private static List<OuroFileRef> MapOutputFiles(IReadOnlyList<BashCodeExecutionOutputBlock>? outputs)
    {
        if (outputs is null)
            return [];

        // Stamped with the provider so DownloadFileAsync knows where to go. Name and media type are
        // absent here - the result block carries only an id - and are fetched at download time.
        return outputs
            .Where(output => !string.IsNullOrWhiteSpace(output.FileID))
            .Select(output => new OuroFileRef(output.FileID) { Provider = OuroProvider.Anthropic })
            .ToList();
    }

    /// <summary>
    /// Pulls the code out of a server tool invocation.
    /// </summary>
    /// <remarks>
    /// Input is a loosely-typed bag on the wire, and the key differs by tool, so this is best
    /// effort. A null Code means "we could not read it", not "there was none" - the execution block
    /// still exists either way.
    /// </remarks>
    private static string? ExtractCode(ServerToolUseBlock toolUse)
    {
        if (toolUse.Input is null || toolUse.Input.Count == 0)
            return null;

        foreach (var key in CodeInputKeys)
        {
            if (toolUse.Input.TryGetValue(key, out var value))
                return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        }

        // Unrecognised shape. Keeping the whole payload beats reporting nothing: this field exists
        // to show a human what ran, and a JSON blob answers that where a null does not.
        return JsonSerializer.Serialize(toolUse.Input);
    }

    /// <summary>
    /// Input keys that have carried the code, most specific first.
    /// </summary>
    /// <remarks>
    /// Which one is used varies by tool version, so this is a list rather than a constant.
    /// </remarks>
    private static readonly string[] CodeInputKeys = ["command", "code", "script", "input"];

    private static string DescribeBlock(ContentBlock block)
    {
        // Best-effort diagnostic only. The union's own ToString is the closest thing to the wire
        // type name without hard-coding a list that will fall out of date.
        return block.ToString() ?? "unknown";
    }

    /// <summary>
    /// Maps Anthropic's stop_reason onto the provider-neutral enum.
    /// </summary>
    /// <remarks>
    /// Matched as strings rather than against the SDK's enum so an unrecognised future value
    /// degrades to Unknown instead of failing to compile or throwing at runtime.
    /// </remarks>
    private static OuroStopReason MapStopReason(string? stopReason)
    {
        return stopReason switch
        {
            "end_turn" => OuroStopReason.EndTurn,
            "max_tokens" => OuroStopReason.MaxTokens,
            "stop_sequence" => OuroStopReason.StopSequence,
            "pause_turn" => OuroStopReason.Paused,
            _ => OuroStopReason.Unknown
        };
    }
}
