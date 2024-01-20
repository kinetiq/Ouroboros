using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.Tokenizer.GPT3;
using Ouroboros.Chaining;
using Ouroboros.Chaining.TemplateDialog;
using Ouroboros.Chaining.TemplateDialog.Templates;
using Ouroboros.Endpoints;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.LargeLanguageModels.Completions;
using Ouroboros.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Ouroboros.LargeLanguageModels.Templates;

[assembly: InternalsVisibleTo("Ouroboros.Test")]

namespace Ouroboros;

public class OuroClient 
{
    private readonly string ApiKey;
    private readonly CompletionRequestHandler CompletionHandler;
    private readonly ChatRequestHandler ChatHandler;
    private readonly TemplateRequestHandler TemplateHandler;

    private OuroModels DefaultCompletionModel = OuroModels.TextDavinciV3;
    private OuroModels DefaultChatModel = OuroModels.Gpt_4;
    private ITemplateEndpoint? DefaultTemplateEndpoint;
    
    /// <summary>
    /// For gaining direct access to a Betalgo client, without going through the OuroClient.
    /// </summary>
    public OpenAIService GetInnerClient => GetClient();

    public Dialog CreateDialog()
    {
        return new Dialog(this);
    }

    #region TemplateDialog & TemplateEndpoints
	public TemplateDialog CreateTemplateDialog()
	{
		return new TemplateDialog(this);
	}

	/// <summary>
	/// Set the TemplateEndpoint for this Ouro Client. All calls to 
	/// </summary>
	/// <param name="endpoint"></param>
	public void SetTemplateEndpoint(ITemplateEndpoint endpoint)
	{
		DefaultTemplateEndpoint = endpoint;
	}

    public async Task<OuroResponseBase> SendTemplateAsync(IOuroTemplateBase templateBase, TemplateOptions? options = null)
    {
        return await SendTemplateAsync(templateBase, null, options);
    }

    public async Task<OuroResponseBase> SendTemplateAsync(IOuroTemplateBase templateBase, ITemplateEndpoint? templateEndpoint = null, TemplateOptions? options = null)
	{
        options ??= new TemplateOptions();
        options.Model ??= DefaultChatModel;
        var api = GetClient();

        var endpoint = DetermineEndpoint(templateEndpoint);

        return await TemplateHandler.SendTemplateAsync(templateBase, endpoint, api, options);
	}

    private ITemplateEndpoint DetermineEndpoint(ITemplateEndpoint? endpoint)
    {
        if (endpoint != null)
            return endpoint;

        if (DefaultTemplateEndpoint == null)
            throw new NotImplementedException("Called SendTemplateAsync without an endpoint parameter, and OuroClient does not have a DefaultTemplateEndpoint set. " +
                                              "You must either set an endpoint on OuroClient using SetTemplateEndpoint, or use the override that takes an endpoint.");

        return DefaultTemplateEndpoint;
    }
    #endregion
    

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

        return await CompletionHandler.Complete(prompt, api, options);
    }

    /// <summary>
    /// Handles a chat completion request.
    /// </summary>
    public async Task<OuroResponseBase> ChatAsync(List<ChatMessage> messages, ChatOptions? options = null)
    {
        options ??= new ChatOptions();
        options.Model ??= DefaultChatModel;

        var api = GetClient();

        return await ChatHandler.CompleteAsync(messages, api, options);
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

    public OuroClient(string apiKey, ITemplateEndpoint? customEndpoint = null)
    {
        // Create an empty services collection, which we need downstream for Polly's retry policy.
        var serviceProvider = new ServiceCollection()
            .BuildServiceProvider();

        CompletionHandler = new CompletionRequestHandler(serviceProvider);
        ChatHandler = new ChatRequestHandler(serviceProvider);
        TemplateHandler = new TemplateRequestHandler(serviceProvider);
        ApiKey = apiKey;
        DefaultTemplateEndpoint = customEndpoint;
    }
}