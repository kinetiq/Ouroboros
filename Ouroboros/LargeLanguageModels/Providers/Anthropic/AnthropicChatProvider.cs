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
using Ouroboros.StructuredOutput;

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

        // Everything the turn has produced so far, across however many requests it took. A paused
        // turn is one turn: its blocks and its token usage belong to a single response.
        var turn = new PausedTurn();

        while (true)
        {
            var parameters = AnthropicMappings.MapOptions(messages, options, turn.Produced);

            Message message;

            try
            {
                message = await client.Messages.Create(parameters, cancellationToken);
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

            turn.Add(message);

            var stopReason = MapStopReason(message.StopReason is { } stop ? (string?)stop : null);

            if (stopReason != OuroStopReason.Paused || turn.Continuations >= Constants.MaxPausedTurnContinuations)
                return ProviderAttempt.Final(turn.ToResponse(message, stopReason, options.ResponseType));

            Logger.LogInformation(
                "Anthropic paused the turn after {Blocks} blocks. Continuing ({Continuation} of {Max}).",
                turn.Produced.Count, turn.Continuations + 1, Constants.MaxPausedTurnContinuations);

            turn.Continue();
        }
    }

    /// <summary>
    /// A turn in progress, gathering what each request produced until the model stops pausing.
    /// </summary>
    /// <remarks>
    /// Anthropic pauses a turn when its own server-side tool loop hits an internal limit. The turn
    /// is not finished and not failed; continuing it means sending back everything produced so far
    /// and asking it to carry on. Without that, a code-execution turn that needed two rounds came
    /// back half-done and successful, which is the worst of both.
    ///
    /// State is kept here rather than in locals because three things have to accumulate together
    /// and stay consistent: the blocks, the token usage, and the count of continuations.
    /// </remarks>
    private sealed class PausedTurn
    {
        private readonly List<ContentBlockParam> ProducedBlocks = [];
        private readonly List<OuroContentBlock> Blocks = [];

        private long PromptTokens;
        private long CompletionTokens;

        /// <summary>
        /// What to send back as the assistant's turn so far. Empty on the first request.
        /// </summary>
        public IReadOnlyList<ContentBlockParam> Produced => ProducedBlocks;

        public int Continuations { get; private set; }

        public void Continue() => Continuations++;

        public void Add(Message message)
        {
            Blocks.AddRange(MapContent(message.Content));

            foreach (var block in message.Content ?? [])
                ProducedBlocks.Add(ToParam(block));

            // Summed, not replaced. Each request bills its own input, and a continuation re-sends
            // the whole turn - so the later requests are the expensive ones. Reporting only the
            // last would under-bill every paused turn.
            PromptTokens += message.Usage?.InputTokens ?? 0;
            CompletionTokens += message.Usage?.OutputTokens ?? 0;
        }

        public OuroResponseBase ToResponse(Message last, OuroStopReason stopReason, System.Type? responseType)
        {
            var success = new OuroResponseSuccess(Blocks)
            {
                // Cast, never ToString(). These SDK wrappers serialise themselves as JSON, so
                // ToString() yields "claude-opus-5" complete with the quotation marks.
                Model = (string?)last.Model ?? "",
                StopReason = stopReason,
                PromptTokens = (int)PromptTokens,
                CompletionTokens = (int)CompletionTokens,
                TotalTokenUsage = (int)(PromptTokens + CompletionTokens)
            };

            success.ResponseObject = ResponseParser.Parse(responseType, success.ResponseText);

            return success;
        }

        /// <summary>
        /// Turns a block the model produced into one that can be sent back to it.
        /// </summary>
        /// <remarks>
        /// Through the block's own raw JSON and the SDK's union converter, rather than a switch over
        /// the dozen block types. Two reasons, and the first is the important one: thinking blocks
        /// carry a signature the API verifies, so a converter that rebuilt them field by field would
        /// reject the whole turn the moment it dropped or reordered anything. Round-tripping the
        /// bytes cannot get that wrong.
        ///
        /// The second is that it carries block types this version has never heard of, which is worth
        /// having on a surface the vendor keeps extending.
        /// </remarks>
        private static ContentBlockParam ToParam(ContentBlock block)
        {
            return JsonSerializer.Deserialize<ContentBlockParam>(block.Json.GetRawText())!;
        }
    }

    /// <summary>
    /// Catches request shapes the provider would accept but not honour, before spending a call.
    /// </summary>
    /// <remarks>
    /// The rules live in ProviderCapabilities so that this refusal and the failover chain's
    /// validation are the same judgement - see the note there.
    /// </remarks>
    private static OuroResponseBase? Reject(ChatOptions options)
    {
        var check = ProviderCapabilities.Check(options, OuroProvider.Anthropic);

        return check.IsSupported ? null : new OuroResponseInternalError(check.Message!);
    }

    private static OuroResponseProviderError Error(string? code, string message)
    {
        return new OuroResponseProviderError("Anthropic", code, message);
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
                // Trimmed to match the joined text OuroResponseSuccess derives from these blocks,
                // so block.Text and ResponseText cannot disagree about whitespace.
                blocks.Add(new OuroTextBlock(text!.Text.Trim()));
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
