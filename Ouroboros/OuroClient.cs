using Betalgo.Ranul.OpenAI;
using Betalgo.Ranul.OpenAI.Managers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Chaining;
using Ouroboros.Config;
using Ouroboros.Core;
using Ouroboros.Extensions;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.LargeLanguageModels.Providers;
using Ouroboros.LargeLanguageModels.Providers.Anthropic;
using Ouroboros.Responses;
using Ouroboros.Tracking;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AnthropicSdk = Anthropic;

[assembly: InternalsVisibleTo("Ouroboros.Test")]

namespace Ouroboros;

public class OuroClient : IOuroClient, IDisposable
{
    private readonly OuroborosOptions Options;

    /// <summary>
    /// Applies Ouroboros' retry, timeout and cancellation policy around whichever provider runs.
    /// </summary>
    private readonly ChatExecutor Executor;

    /// <summary>
    /// When set, every request goes here regardless of model. Test seam only.
    /// </summary>
    private readonly IChatProvider? ProviderOverride;

    /// <summary>
    /// Where HookFailurePolicy.Log sends hook failures. Falls back to NullLogger, which is why
    /// that policy is only as visible as the consumer's logging setup.
    /// </summary>
    private readonly ILogger<OuroClient> Logger;

    /// <summary>
    /// One transport and one provider client each, built on first use.
    /// </summary>
    /// <remarks>
    /// Separate HttpClients per provider on purpose - SDKs set their own auth headers on the client
    /// they are handed, so sharing one would send an OpenAI key to Anthropic.
    ///
    /// Both run with no timeout of their own. HttpClient.Timeout is per-client and cannot express a
    /// per-attempt budget; the real deadline is applied per attempt by ChatExecutor.
    /// </remarks>
    private readonly Lazy<HttpClient> OpenAiTransport;

    private readonly Lazy<HttpClient> AnthropicTransport;
    private readonly Lazy<IChatProvider> OpenAiProvider;
    private readonly Lazy<IChatProvider> AnthropicProvider;

    /// <summary>
    /// Anthropic's file store, sharing the transport and credentials of the chat provider.
    /// </summary>
    private readonly Lazy<AnthropicFileStore> AnthropicFiles;

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
    ///
    /// OpenAI models only. Anthropic publishes no tokenizer, so Claude models throw rather than
    /// return a guess - read the provider's own usage off the response instead.
    /// </remarks>
    public static int TokenCount(string text, OuroModels model)
    {
        return Tokenization.CountTokens(text, model);
    }

    /// <summary>
    /// Handles a chat completion request.
    /// </summary>
    public async Task<OuroResponseBase> ChatAsync(List<OuroMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ChatOptions();

        if (options.Model == null)
        {
            options.Model = DefaultChatModel;
            options.ReasoningEffort = DefaultReasoningEffort;
        }

        var provider = ResolveProvider(options.Model.Value);

        var stopwatch = Stopwatch.StartNew();
        var response = await Executor.ExecuteAsync(provider, messages, options, cancellationToken);
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
    /// Uploads a file to a provider's store so the model can work with it.
    /// </summary>
    public async Task<OuroFileRef> UploadFileAsync(byte[] content, string fileName, string? mediaType,
        OuroProvider provider, CancellationToken cancellationToken = default)
    {
        if (content is null)
            throw new ArgumentNullException(nameof(content));

        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("A file name is required.", nameof(fileName));

        return await ResolveFileStore(provider).UploadAsync(content, fileName, mediaType, cancellationToken);
    }

    /// <summary>
    /// Fetches a file's bytes, along with the filename and media type the provider reports for it.
    /// </summary>
    public async Task<OuroFileContent> DownloadFileAsync(OuroFileRef file,
        CancellationToken cancellationToken = default)
    {
        if (file is null)
            throw new ArgumentNullException(nameof(file));

        return await ResolveFileStore(file.Provider).DownloadAsync(file, cancellationToken);
    }

    /// <summary>
    /// Deletes a file from the provider's store.
    /// </summary>
    public async Task DeleteFileAsync(OuroFileRef file, CancellationToken cancellationToken = default)
    {
        if (file is null)
            throw new ArgumentNullException(nameof(file));

        await ResolveFileStore(file.Provider).DeleteAsync(file, cancellationToken);
    }

    /// <summary>
    /// Picks the file store for a provider.
    /// </summary>
    /// <remarks>
    /// A file reference belongs to the store that issued it, so routing on the reference rather
    /// than on a model is what stops an Anthropic id being sent to OpenAI and coming back as a
    /// baffling 404.
    /// </remarks>
    private AnthropicFileStore ResolveFileStore(OuroProvider provider)
    {
        return provider switch
        {
            OuroProvider.Anthropic => AnthropicFiles.Value,

            OuroProvider.OpenAi => throw new NotSupportedException(
                "File upload and download are not implemented for OpenAI yet. They arrive with the "
                + "move from Chat Completions to the Responses API, which is where OpenAI's "
                + "server-side tools live."),

            _ => throw new NotSupportedException($"No file store is wired up for {provider}.")
        };
    }

    /// <summary>
    /// Picks the provider that serves this model.
    /// </summary>
    private IChatProvider ResolveProvider(OuroModels model)
    {
        if (ProviderOverride is not null)
            return ProviderOverride;

        return model.GetProvider() switch
        {
            OuroProvider.OpenAi => OpenAiProvider.Value,
            OuroProvider.Anthropic => AnthropicProvider.Value,
            var unknown => throw new NotSupportedException(
                $"{model} is served by {unknown}, which this version has no client for.")
        };
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

    private static string RequireKey(string? key, OuroProvider provider)
    {
        if (!string.IsNullOrWhiteSpace(key))
            return key;

        throw new InvalidOperationException(
            $"A {provider} model was requested but no {provider} API key is configured. Supply one " +
            "through the AddOuroboros overload that takes OuroborosOptions.");
    }

    /// <summary>
    /// Releases the HTTP transports. Only IOuroClient is on the public contract, so in practice the
    /// DI container disposes this at the end of the scope that resolved it.
    /// </summary>
    public void Dispose()
    {
        // Lazy, so a provider that was never called has nothing to release.
        if (OpenAiTransport.IsValueCreated)
            OpenAiTransport.Value.Dispose();

        if (AnthropicTransport.IsValueCreated)
            AnthropicTransport.Value.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Creates a client for OpenAI only. Use the OuroborosOptions overload to reach Claude models.
    /// </summary>
    public OuroClient(string apiKey, ILogger<OuroClient>? logger = null)
        : this(new OuroborosOptions { OpenAiApiKey = apiKey }, logger)
    {
    }

    public OuroClient(OuroborosOptions options, ILogger<OuroClient>? logger = null)
        : this(options, null, logger)
    {
    }

    internal OuroClient(OuroborosOptions options, IChatProvider? providerOverride,
        ILogger<OuroClient>? logger = null)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        ProviderOverride = providerOverride;
        Logger = logger ?? NullLogger<OuroClient>.Instance;
        Executor = new ChatExecutor(Logger);

        OpenAiTransport = new Lazy<HttpClient>(() =>
            new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan });

        AnthropicTransport = new Lazy<HttpClient>(() =>
            new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan });

        OpenAiProvider = new Lazy<IChatProvider>(() => new OpenAiChatProvider(
            new OpenAIService(
                new OpenAIOptions { ApiKey = RequireKey(Options.OpenAiApiKey, OuroProvider.OpenAi) },
                OpenAiTransport.Value),
            Logger));

        // One SDK client shared by chat and files - same credentials, same transport, and the
        // file ids only mean anything to the account that issued them.
        var anthropic = new Lazy<AnthropicSdk.AnthropicClient>(() => new AnthropicSdk.AnthropicClient
        {
            ApiKey = RequireKey(Options.AnthropicApiKey, OuroProvider.Anthropic),
            HttpClient = AnthropicTransport.Value,

            // The SDK retries 429s and 5xx twice by default. Left on, that multiplies against
            // Polly rather than replacing it - up to six attempts where the caller asked for
            // two. ChatExecutor is the single retry authority.
            MaxRetries = 0
        });

        AnthropicProvider = new Lazy<IChatProvider>(() => new AnthropicChatProvider(anthropic.Value, Logger));

        AnthropicFiles = new Lazy<AnthropicFileStore>(() => new AnthropicFileStore(anthropic.Value));
    }
}
