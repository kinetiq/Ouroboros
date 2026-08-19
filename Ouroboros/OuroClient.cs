using System.ClientModel;
using System.ClientModel.Primitives;
using OpenAI;
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
using Ouroboros.LargeLanguageModels.Providers.OpenAi;
using Ouroboros.Responses;
using Ouroboros.Tracking;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
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
    /// When set, supplies the provider for a model instead of the real clients. Test seam only.
    /// </summary>
    /// <remarks>
    /// Keyed by model so a test can give each chain entry a different provider. One provider for
    /// every model cannot express a fallback chain at all.
    /// </remarks>
    private readonly Func<OuroModels, IChatProvider>? ProviderOverride;

    /// <summary>
    /// Where HookFailurePolicy.Log sends hook failures. Falls back to NullLogger, which is why
    /// that policy is only as visible as the consumer's logging setup.
    /// </summary>
    private readonly ILogger<OuroClient> Logger;

    /// <summary>
    /// One transport and one provider client each, built on first use.
    /// </summary>
    /// <remarks>
    /// One HttpClient per provider, never shared. Each SDK sets its own auth headers on the client
    /// it is given, so a shared one would send an OpenAI key to Anthropic.
    ///
    /// Neither has a timeout. HttpClient.Timeout is per client and cannot express a per-attempt
    /// budget, so ChatExecutor applies the real deadline to each attempt.
    /// </remarks>
    private readonly Lazy<HttpClient> OpenAiTransport;

    private readonly Lazy<HttpClient> AnthropicTransport;
    private readonly Lazy<IChatProvider> OpenAiProvider;
    private readonly Lazy<IChatProvider> AnthropicProvider;

    /// <summary>
    /// The providers' file stores, each sharing the transport and credentials of its chat provider.
    /// </summary>
    private readonly Lazy<AnthropicFileStore> AnthropicFiles;

    private readonly Lazy<OpenAiFileStore> OpenAiFiles;

    private OuroModels DefaultChatModel = Constants.DefaultChatModel;

    /// <summary>
    /// Note that this only gets applied if DefaultChatModel is used. These defaults
    /// are intended to be a set; wouldn't want to overwrite an intentionally null reasoning effort.
    /// </summary>
    private OuroReasoningEffort? DefaultReasoningEffort = Constants.DefaultReasoningEffort;

    /// <summary>
    /// Models every call falls back to, unless it says otherwise. Empty means no failover.
    /// </summary>
    private IReadOnlyList<OuroModels> DefaultFallbackModels = [];

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
    /// The model matters. Families tokenize the same text differently, so a count taken against the
    /// wrong one is simply wrong. OpenAI models only; see <see cref="CanCountTokens" />.
    /// </remarks>
    public static int TokenCount(string text, OuroModels model)
    {
        return Tokenization.CountTokens(text, model);
    }

    /// <summary>
    /// Whether <see cref="TokenCount" /> works for this model. False for Claude models - Anthropic
    /// publishes no tokenizer, so there is no honest local count to give.
    /// </summary>
    /// <remarks>
    /// For opportunistic counters - loggers estimating message sizes, UI showing rough usage -
    /// check this and skip rather than catching NotSupportedException per message. The provider's
    /// own usage is always on the response (PromptTokens / CompletionTokens) regardless.
    /// </remarks>
    public static bool CanCountTokens(OuroModels model)
    {
        return Tokenization.CanCount(model);
    }

    /// <summary>
    /// Handles a chat completion request.
    /// </summary>
    public async Task<OuroResponseBase> ChatAsync(List<OuroMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Work on our own copy. Resolving defaults onto the caller's instance meant a reused
        // ChatOptions silently kept the first call's model forever, and a later
        // SetDefaultChatModel never reached it.
        options = options?.Clone() ?? new ChatOptions();

        if (options.Model == null)
        {
            options.Model = DefaultChatModel;
            options.ReasoningEffort = DefaultReasoningEffort;
        }

        // Built and checked before anything is sent, so a chain that cannot do what the call asks
        // says so now rather than after the primary attempt has already been paid for.
        var (chain, unreachable) = BuildChain(options);

        if (unreachable is not null)
            return unreachable;

        if (Validate(chain, messages, options) is { } refusal)
            return refusal;

        var stopwatch = Stopwatch.StartNew();
        var result = await Executor.ExecuteChainAsync(chain, messages, cancellationToken);
        stopwatch.Stop();

        // The returned response reports the whole call, every attempt included. Each attempt's own
        // duration stays on its record, which is what the hook reports.
        result.FinalResponse.DurationMs = (int)stopwatch.ElapsedMilliseconds;

        await FireCompletedHooks(result, messages, options);

        // Deferred to here so the attempts that did complete are logged first. Everything above is
        // bookkeeping for work already done and paid for; this is the caller's own signal.
        cancellationToken.ThrowIfCancellationRequested();

        return result.FinalResponse;
    }

    /// <summary>
    /// Fires OnChatCompleted once per completed attempt, in order.
    /// </summary>
    /// <remarks>
    /// Awaited one at a time. A consumer's logger is often a single stateful instance, and
    /// overlapping calls would interleave two chats into one row.
    ///
    /// Under HookFailurePolicy.Throw every hook still runs, and the first exception is raised after
    /// the loop. Stopping at the first would lose the successful attempt's response to a fault
    /// while logging the failed one.
    /// </remarks>
    private async Task FireCompletedHooks(ChainResult result, List<OuroMessage> messages, ChatOptions options)
    {
        if (OnChatCompleted is null || result.Attempts.Count == 0)
            return;

        Exception? firstToRethrow = null;

        for (var index = 0; index < result.Attempts.Count; index++)
        {
            var attempt = result.Attempts[index];
            var isLast = index == result.Attempts.Count - 1;

            var args = new ChatCompletedArgs(
                options.PromptName,
                options.Session?.SessionId,
                options.Thread?.ThreadId,
                messages,
                attempt.Response,
                attempt.Model,
                options.ReasoningEffort,
                attempt.DurationMs,
                options.Thread?.Tags ?? [],
                options.Session?.Tags ?? [],
                options.Variables,
                index + 1,
                isLast ? null : result.Attempts[index + 1].Model
            );

            try
            {
                await OnChatCompleted(args);
            }
            catch (Exception ex)
            {
                if (ReportHookFailure(ex, args))
                    firstToRethrow ??= ex;
            }
        }

        if (firstToRethrow is not null)
            ExceptionDispatchInfo.Capture(firstToRethrow).Throw();
    }

    /// <summary>
    /// Resolves the models this call may run on, in order, with a provider for each.
    /// </summary>
    /// <remarks>
    /// A fallback naming a provider with no API key can only get here from this call's
    /// ChatOptions.FallbackModels, and the call fails with an error naming the provider. Nothing is
    /// spent finding out.
    ///
    /// A client-wide default cannot be in that state: OuroborosOptions.Validate refuses it when the
    /// host starts, and SetDefaultFallback refuses it when called. Configuration is known early, so
    /// it is checked early - a fallback that was quietly discarded would be discovered during the
    /// outage it was configured for.
    ///
    /// The primary model is covered by neither rule. A call naming a model whose provider has no key
    /// throws, exactly as it did before failover existed.
    /// </remarks>
    private (List<ChainEntry> Chain, OuroResponseBase? Refusal) BuildChain(ChatOptions options)
    {
        var primary = options.Model!.Value; // always resolved by the caller
        var models = new List<OuroModels> { primary };

        // Null means "whatever the client was configured with"; an empty list means the caller
        // explicitly wants no failover, and beats the client default.
        var callerChose = options.FallbackModels is not null;

        IEnumerable<OuroModels> fallbacks = options.FallbackModels is { } chosen
            ? chosen
            : DefaultFallbackModels;

        foreach (var fallback in fallbacks)
        {
            if (models.Contains(fallback))
            {
                Logger.LogDebug("Skipping {Model} in the fallback chain; it is already in it.", fallback);
                continue;
            }

            // Only a per-call chain can reach this. A client default naming a provider with no
            // key cannot be configured at all - OuroborosOptions.Validate refuses it at startup, and
            // SetDefaultFallback refuses it at the point of the call.
            if (callerChose && !Options.HasKeyFor(fallback.GetProvider()))
            {
                return ([], new OuroResponseInternalError(
                    $"{fallback} is in this call's fallback chain, but no {fallback.GetProvider()} "
                    + "API key is configured on this client. Supply one through OuroborosOptions, or "
                    + "take the model out of the chain."));
            }

            models.Add(fallback);
        }

        var chain = models
            .Select(model => new ChainEntry(model, ResolveProvider(model), OptionsFor(options, model)))
            .ToList();

        return (chain, null);

        ChatOptions OptionsFor(ChatOptions source, OuroModels model)
        {
            // Each entry carries its own model, so the mapper stamps the right id on the request.
            var entry = source.Clone();
            entry.Model = model;

            return entry;
        }
    }

    /// <summary>
    /// Checks every entry can serve this call, before any of them is asked to.
    /// </summary>
    /// <remarks>
    /// A single-model call skips this entirely. The provider refuses it on its own, and callers who
    /// never asked for failover must behave exactly as they did before.
    ///
    /// For longer chains, where the chain came from decides how strict to be. A chain the caller
    /// wrote for this call is their intent, so a conflict is an error naming the option. A chain
    /// from the client default is not about this call, so the offending entry is dropped with a
    /// log. BuildChain applies the same split to a missing API key.
    /// </remarks>
    private OuroResponseBase? Validate(List<ChainEntry> chain, List<OuroMessage> messages, ChatOptions options)
    {
        if (chain.Count <= 1)
            return null;

        var callerChose = options.FallbackModels is not null;

        for (var index = chain.Count - 1; index >= 0; index--)
        {
            var entry = chain[index];
            var check = ProviderCapabilities.Check(messages, entry.Options, entry.Provider.Kind);

            if (check.IsSupported)
                continue;

            // Wrong however it is routed. Failing over would only spend money confirming that.
            if (check.Verdict == CapabilityVerdict.Invalid)
                return new OuroResponseInternalError(check.Message!);

            // The primary is the call the caller actually asked for; a chain must never silently
            // drop it. Its own provider will refuse it with the same message.
            if (index == 0)
                return new OuroResponseInternalError(check.Message!);

            // AllowDegraded is a standing instruction to do the best this chain can: run entries
            // without what they cannot express, and drop the ones that cannot be salvaged that way.
            if (options.AllowDegraded)
            {
                if (check.Verdict == CapabilityVerdict.Degradable)
                {
                    chain[index] = entry with { Options = ProviderCapabilities.Degrade(entry.Options, entry.Provider.Kind) };

                    Logger.LogInformation(
                        "{Model} cannot serve this call as asked, and AllowDegraded is set, so it will "
                        + "run without that option. {Detail}", entry.Model, check.Message);

                    continue;
                }

                // NotServable, so there is nothing to strip that would leave the same question
                // being asked. Dropping the entry shortens the chain; degrading it would mean a
                // model answering about a file it cannot see, which comes back looking like success.
                Drop(chain, index, entry, check.Message);
                continue;
            }

            if (!callerChose)
            {
                Drop(chain, index, entry, check.Message);
                continue;
            }

            return new OuroResponseInternalError(
                $"{check.Message} It is in this call's fallback chain as {entry.Model}. Remove it, "
                + "route around it, or set ChatOptions.AllowDegraded to run the chain for whatever "
                + "it can still serve.");
        }

        return null;

        void Drop(List<ChainEntry> entries, int at, ChainEntry dropped, string? detail)
        {
            Logger.LogInformation(
                "Dropping {Model} from the fallback chain for this call: {Detail}", dropped.Model, detail);

            entries.RemoveAt(at);
        }
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
    /// Routed on the reference, not on a model. A file id only means something to the store that
    /// issued it, and one sent to the wrong vendor returns a 404 for an id you can see exists.
    /// </remarks>
    private IProviderFileStore ResolveFileStore(OuroProvider provider)
    {
        return provider switch
        {
            OuroProvider.Anthropic => AnthropicFiles.Value,
            OuroProvider.OpenAi => OpenAiFiles.Value,

            _ => throw new NotSupportedException($"No file store is wired up for {provider}.")
        };
    }

    /// <summary>
    /// Picks the provider that serves this model.
    /// </summary>
    private IChatProvider ResolveProvider(OuroModels model)
    {
        if (ProviderOverride is not null)
            return ProviderOverride(model);

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

    /// <summary>
    /// Configures the models every chat falls back to, in order, when the one it asked for cannot
    /// serve it.
    /// </summary>
    /// <remarks>
    /// Only transient failures move down the chain; ChatOptions.FallbackModels lists exactly which.
    ///
    /// A single call overrides this by setting FallbackModels itself. Setting that to an empty list
    /// opts one call out of failover; calling this method with no arguments turns it off for the
    /// whole client.
    ///
    /// Reasoning effort is not per model. Whatever effort a request carries is used for every entry
    /// in its chain - including an effort passed to SetDefaultChatModel, which was chosen for that
    /// model rather than for these.
    ///
    /// A model here whose provider has no API key throws immediately. Prefer
    /// OuroborosOptions.FallbackModels, which is validated when the host starts rather than when
    /// this client is first resolved.
    /// </remarks>
    public void SetDefaultFallback(params OuroModels[] models)
    {
        foreach (var model in models ?? [])
        {
            if (Options.HasKeyFor(model.GetProvider()))
                continue;

            throw new InvalidOperationException(
                $"{model} cannot be a fallback: no {model.GetProvider()} API key is configured on "
                + "this client. Configure one, or declare the chain through "
                + "OuroborosOptions.FallbackModels, which is checked when the host starts.");
        }

        DefaultFallbackModels = models ?? [];
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

    internal OuroClient(OuroborosOptions options, Func<OuroModels, IChatProvider>? providerOverride,
        ILogger<OuroClient>? logger = null)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));

        // AddOuroboros already did this at registration. Repeated here because a client can be
        // constructed directly, and the failure should look the same either way.
        Options.Validate();

        DefaultFallbackModels = [.. options.FallbackModels ?? []];
        ProviderOverride = providerOverride;
        Logger = logger ?? NullLogger<OuroClient>.Instance;
        Executor = new ChatExecutor(Logger);

        OpenAiTransport = new Lazy<HttpClient>(() =>
            new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan });

        AnthropicTransport = new Lazy<HttpClient>(() =>
            new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan });

        // One configured client, three sub-clients off it: chat, the account-wide file store, and
        // containers. Configuring each separately would work and would be a standing invitation to
        // set the pipeline up correctly on two of them and not the third.
        var openAi = new Lazy<OpenAIClient>(() => new OpenAIClient(
            new ApiKeyCredential(RequireKey(Options.OpenAiApiKey, OuroProvider.OpenAi)),
            new OpenAIClientOptions
            {
                Transport = new HttpClientPipelineTransport(OpenAiTransport.Value),

                // Two separate timeouts, and both have to be off. HttpClient.Timeout is the
                // obvious one; NetworkTimeout is a further per-attempt cancellation the pipeline
                // applies, defaulting to 100s - left alone it would cap ChatOptions.Timeout
                // invisibly, which is the trap the transport alone misses.
                NetworkTimeout = System.Threading.Timeout.InfiniteTimeSpan,

                // The SDK retries three times by default. Left on, that multiplies against Polly
                // rather than replacing it. ChatExecutor is the single retry authority - the same
                // rule applied to the Anthropic client.
                RetryPolicy = new ClientRetryPolicy(maxRetries: 0)
            }));

        OpenAiProvider = new Lazy<IChatProvider>(() =>
            new OpenAiResponsesProvider(openAi.Value.GetResponsesClient(), Logger));

        OpenAiFiles = new Lazy<OpenAiFileStore>(() => new OpenAiFileStore(
            openAi.Value.GetOpenAIFileClient(), openAi.Value.GetContainerClient()));

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
