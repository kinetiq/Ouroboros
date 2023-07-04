﻿using AI.Dev.OpenAI.GPT;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.Completions;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OpenAI.Managers;
using OpenAI;
using OpenAI.ObjectModels.RequestModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;

[assembly: InternalsVisibleTo("Ouroboros.Test")]

namespace Ouroboros;

public class OuroClient 
{
    private readonly string ApiKey;
    private OuroModels DefaultCompletionModel = OuroModels.TextDavinciV3;
    private OuroModels DefaultChatModel = OuroModels.Gpt_4;
    
    /// <summary>
    /// For gaining direct access to a Betalgo client, without going through the OuroClient.
    /// </summary>
    public OpenAIService GetInnerClient => GetClient();

    /// <summary>
    /// Coverts text into tokens. Uses GPT3Tokenizer.
    /// </summary>
    public List<int> Tokenize(string text)
    {
        return GPT3Tokenizer.Encode(text);
    }

    /// <summary>
    /// Gets the number of tokens the given text would take up. Uses GPT3Tokenizer.
    /// </summary>
    public int TokenCount(string text)
    {
        var tokens = Tokenize(text);

        return tokens.Count;
    }

    /// <summary>
    /// Handles a text completion request.
    /// </summary>
    public async Task<CompleteResponseBase> CompleteAsync(string prompt, CompleteOptions? options = null)
    {
        options ??= new CompleteOptions();
        options.Model ??= DefaultCompletionModel;
        var api = GetClient();

        var handler = new CompletionRequestHandler(api);

        return await handler.Complete(prompt, options);
    }

    /// <summary>
    /// Handles a chat completion request.
    /// </summary>
    public async Task<CompleteResponseBase> ChatAsync(List<ChatMessage> messages, ChatOptions? options = null)
    {
        var api = GetClient();
        var handler = new ChatRequestHandler(api);

        return await handler.CompleteAsync(messages, options);
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
    public void SetDefaultChatModel(OuroModels model)
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
        return new OpenAIService(new OpenAiOptions
        {
            ApiKey = ApiKey
        });
    }

    public OuroClient(string apiKey)
    {
        ApiKey = apiKey;
    }
}