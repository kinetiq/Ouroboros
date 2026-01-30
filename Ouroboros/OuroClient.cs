using Betalgo.Ranul.OpenAI;
using Betalgo.Ranul.OpenAI.Managers;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Betalgo.Ranul.OpenAI.Tokenizer.GPT3;
using Ouroboros.Chaining;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.LargeLanguageModels.Completions;
using Ouroboros.Responses;
using Ouroboros.Tracking;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Betalgo.Ranul.OpenAI.Contracts.Enums;

[assembly: InternalsVisibleTo("Ouroboros.Test")]

namespace Ouroboros;

public class OuroClient 
{
    private readonly string ApiKey;
    private readonly CompletionRequestHandler CompletionHandler;
    private readonly ChatRequestHandler ChatHandler;

    private OuroModels DefaultCompletionModel = Constants.DefaultCompletionModel;
    private OuroModels DefaultChatModel = Constants.DefaultChatModel;

    /// <summary>
    /// Note that this only gets applied if DefaultChatModel is used. These defaults
    /// are intended to be a set; wouldn't want to overwrite an intentionally null reasoning effort.
    /// </summary>
    private ReasoningEffort? DefaultReasoningEffort = Constants.DefaultReasoningEffort;

    /// <summary>
    /// For gaining direct access to a Betalgo client, without going through the OuroClient.
    /// </summary>
    public OpenAIService GetInnerClient => GetClient();

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
    /// Coverts text into tokens. Uses GPT3Tokenizer.
    /// </summary>
    public static List<int> Tokenize(string text)
    {
        var tokens = TokenizerGpt3.Encode(text, cleanUpCREOL: true); // cleanup improves accuracy

        return tokens.ToList();
    }

    /// <summary>
    /// Gets the number of tokens the given text would take up. Uses GPT3Tokenizer.
    /// </summary>
    public static int TokenCount(string text)
    {
        var tokens = Tokenize(text);

        return tokens.Count;
    }

    /// <summary>
    /// Handles a text completion request.
    /// </summary>
    public async Task<OuroResponseBase> CompleteAsync(string prompt, CompleteOptions? options = null)
    {
        options ??= new CompleteOptions();
        options.Model ??= DefaultCompletionModel;
        var api = GetClient();

        return await CompletionHandler.CompleteAsync(prompt, api, options);
    }

    /// <summary>
    /// Handles a chat completion request.
    /// </summary>
    public async Task<OuroResponseBase> ChatAsync(List<ChatMessage> messages, ChatOptions? options = null)
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
                options.SessionId,
                options.ThreadId,
                messages,
                response,
                options.ReasoningEffort,
                durationMs
            );

            await OnChatCompleted(args);
        }

        return response;
    }

    /// <summary>
    /// Configures a default model that will be used for all completions initiated from this client,
    /// unless overriden by passing in a model via CompleteOptions.
    /// </summary>
    public void SetDefaultCompletionModel(OuroModels model)
    {
        DefaultCompletionModel = model;
    }

    /// <summary>
    /// Configures a default model that will be used for all completions initiated from this client,
    /// unless overriden by passing in a model via CompleteOptions.
    /// </summary>
    public void SetDefaultChatModel(OuroModels model, ReasoningEffort? reasoningEffort)
    {
        DefaultChatModel = model;
    }

    private CompleteOptions ConfigureOptions(CompleteOptions? options)
    {
        options ??= new CompleteOptions();
        options.Model ??= DefaultCompletionModel;

        return options;
    }

    private ChatOptions ConfigureOptions(ChatOptions? options)
    {
        options ??= new ChatOptions();
        options.Model ??= DefaultChatModel;

        return options;
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
        CompletionHandler = new CompletionRequestHandler(null);
        ChatHandler = new ChatRequestHandler(null);
        ApiKey = apiKey;
    }

    internal OuroClient(string apiKey, CompletionRequestHandler completionHandler, ChatRequestHandler chatHandler)
    {
        ApiKey = apiKey;
        CompletionHandler = completionHandler;
        ChatHandler = chatHandler;
    }
}