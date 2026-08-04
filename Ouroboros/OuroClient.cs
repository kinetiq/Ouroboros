using Betalgo.Ranul.OpenAI;
using Betalgo.Ranul.OpenAI.Managers;
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

    private OuroModels DefaultChatModel = Constants.DefaultChatModel;

    /// <summary>
    /// Note that this only gets applied if DefaultChatModel is used. These defaults
    /// are intended to be a set; wouldn't want to overwrite an intentionally null reasoning effort.
    /// </summary>
    private OuroReasoningEffort? DefaultReasoningEffort = Constants.DefaultReasoningEffort;

    /// <summary>
    /// Event fired after every ChatAsync call completes. Use for centralized logging.
    /// </summary>
    public Func<ChatCompletedArgs, Task>? OnChatCompleted { get; set; }

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

            await OnChatCompleted(args);
        }

        return response;
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

    public OuroClient(string apiKey)
    {
        ChatHandler = new ChatRequestHandler(null);
        ApiKey = apiKey;
    }

    internal OuroClient(string apiKey, ChatRequestHandler chatHandler)
    {
        ApiKey = apiKey;
        ChatHandler = chatHandler;
    }
}