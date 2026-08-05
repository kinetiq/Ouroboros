using Betalgo.Ranul.OpenAI;
using Betalgo.Ranul.OpenAI.Managers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Chaining;
using Ouroboros.Core;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.Responses;
using Ouroboros.Tracking;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("Ouroboros.Test")]

namespace Ouroboros;

public class OuroClient : IOuroClient
{
    private readonly string ApiKey;
    private readonly ChatRequestHandler ChatHandler;

    /// <summary>
    /// Where HookFailurePolicy.Log sends hook failures. Falls back to NullLogger, which is why
    /// that policy is only as visible as the consumer's logging setup.
    /// </summary>
    private readonly ILogger<OuroClient> Logger;

    private OuroModels DefaultChatModel = Constants.DefaultChatModel;

    /// <summary>
    /// Note that this only gets applied if DefaultChatModel is used. These defaults
    /// are intended to be a set; wouldn't want to overwrite an intentionally null reasoning effort.
    /// </summary>
    private OuroReasoningEffort? DefaultReasoningEffort = Constants.DefaultReasoningEffort;

    /// <summary>
    /// Event fired after every ChatAsync call completes. Use for centralized logging.
    /// </summary>
    /// <remarks>
    /// Exceptions thrown here do not fail the chat by default - see OnChatCompletedFailure.
    /// </remarks>
    public Func<ChatCompletedArgs, Task>? OnChatCompleted { get; set; }

    /// <summary>
    /// What to do when OnChatCompleted throws: HookFailurePolicy.Log (the default), .Throw,
    /// .Ignore, or .Handle(yourHandler).
    /// </summary>
    public HookFailurePolicy OnChatCompletedFailure { get; set; } = HookFailurePolicy.Log;

    public Dialog CreateDialog()
    {
        return new Dialog(this);
    }

    public Dialog CreateDialog(string promptName)
    {
        return new Dialog(this, promptName);
    }

    public Dialog CreateDialog(DialogOptions options)
    {
        return new Dialog(this, options);
    }

    /// <summary>
    /// Gets the number of tokens the given text would take up for the given model.
    /// </summary>
    /// <remarks>
    /// The model matters: different families tokenize the same text differently, so a count
    /// taken against the wrong model is simply wrong.
    /// </remarks>
    public static int TokenCount(string text, OuroModels model)
    {
        return Tokenization.CountTokens(text, model);
    }

    /// <summary>
    /// Handles a chat completion request.
    /// </summary>
    public async Task<OuroResponseBase> ChatAsync(List<OuroMessage> messages, ChatOptions? options = null)
    {
        options ??= new ChatOptions();

        if (options.Model == null)
        {
            options.Model = DefaultChatModel;
            options.ReasoningEffort = DefaultReasoningEffort;
        }

        var api = GetClient();

        var stopwatch = Stopwatch.StartNew();
        var response = await ChatHandler.CompleteAsync(messages, api, options);
        stopwatch.Stop();

        var durationMs = (int)stopwatch.ElapsedMilliseconds;
        response.DurationMs = durationMs;

        // Fire the OnChatCompleted hook for logging
        if (OnChatCompleted != null)
        {
            var args = new ChatCompletedArgs(
                options.PromptName,
                options.Session?.SessionId,
                options.Thread?.ThreadId,
                messages,
                response,
                options.Model!.Value, // always resolved above
                options.ReasoningEffort,
                durationMs,
                options.Thread?.Tags ?? [],
                options.Session?.Tags ?? [],
                options.Variables
            );

            try
            {
                await OnChatCompleted(args);
            }
            catch (Exception ex)
            {
                // Rethrow from inside the catch rather than from the helper, so the original
                // stack trace survives.
                if (ReportHookFailure(ex, args))
                    throw;
            }
        }

        return response;
    }

    /// <summary>
    /// Applies OnChatCompletedFailure to a hook that threw. Returns true if the exception should
    /// propagate out of ChatAsync.
    /// </summary>
    private bool ReportHookFailure(Exception ex, ChatCompletedArgs args)
    {
        // Defensive: the property is non-nullable, but nothing stops a caller assigning null
        // through a null-oblivious context, and losing the response over that would be absurd.
        var policy = OnChatCompletedFailure ?? HookFailurePolicy.Log;

        switch (policy.Behavior)
        {
            case HookFailureBehavior.Throw:
                return true;

            case HookFailureBehavior.Ignore:
                return false;

            case HookFailureBehavior.Handle:
                try
                {
                    policy.Handler!(ex, args);
                }
                catch (Exception handlerEx)
                {
                    // The handler is the last line of reporting. Letting it escape would defeat
                    // the guard, so fall back to the logger and give up after that.
                    LogHookFailure(handlerEx, args, "Its OnChatCompletedFailure handler then threw as well.");
                }

                return false;

            default:
                LogHookFailure(ex, args, "");
                return false;
        }
    }

    private void LogHookFailure(Exception ex, ChatCompletedArgs args, string extra)
    {
        Logger.LogError(
            ex,
            "The OnChatCompleted hook threw for prompt {PromptName} on model {Model}. The chat itself " +
            "succeeded and its response was returned to the caller. {Extra}",
            args.PromptName ?? "(unnamed)",
            args.Model,
            extra);
    }

    /// <summary>
    /// Configures a default model that will be used for all chats initiated from this client,
    /// unless overridden by passing in a model via ChatOptions.
    /// </summary>
    public void SetDefaultChatModel(OuroModels model, OuroReasoningEffort? reasoningEffort = null)
    {
        DefaultChatModel = model;
        DefaultReasoningEffort = reasoningEffort;
    }

    internal OpenAIService GetClient()
    {
        return new OpenAIService(new OpenAIOptions()
        {
            ApiKey = ApiKey
        });
    }

    public OuroClient(string apiKey, ILogger<OuroClient>? logger = null)
    {
        ChatHandler = new ChatRequestHandler(null);
        ApiKey = apiKey;
        Logger = logger ?? NullLogger<OuroClient>.Instance;
    }

    internal OuroClient(string apiKey, ChatRequestHandler chatHandler, ILogger<OuroClient>? logger = null)
    {
        ApiKey = apiKey;
        ChatHandler = chatHandler;
        Logger = logger ?? NullLogger<OuroClient>.Instance;
    }
}