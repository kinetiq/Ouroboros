// OuroClient is not experimental, but this part of it is. Suppressed for this file only, so the
// rest of OuroClient still fails to build if it reaches into the Decisions surface by accident.
#pragma warning disable OURO001

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Ouroboros.Decisions;
using Ouroboros.LargeLanguageModels;
using Ouroboros.LargeLanguageModels.Providers.OpenAi;
using Ouroboros.LargeLanguageModels.Resilience;
using Ouroboros.Responses;

namespace Ouroboros;

/// <summary>
/// The Decisions API, kept in its own file so the beta surface stays in one place.
/// </summary>
/// <remarks>
/// Does not fire OnChatCompleted. ChatCompletedArgs describes a chat - messages, a model enum,
/// content blocks - and a decision has none of those. A decision hook can come when the API is GA.
/// </remarks>
public partial class OuroClient : IOuroDecisions
{
    /// <summary>
    /// Built on first use, so a client that never decides needs no OpenAI key for it.
    /// </summary>
    /// <remarks>
    /// Not locked. Two threads racing here build two equal providers over the same transport, and
    /// one is dropped. That costs nothing, so a lock would only add a way to deadlock.
    /// </remarks>
    private OpenAiDecisionsProvider? DecisionsProviderInstance;

    private OpenAiDecisionsProvider DecisionsProvider => DecisionsProviderInstance ??=
        new OpenAiDecisionsProvider(OpenAiTransport.Value, RequireKey(Options.OpenAiApiKey, OuroProvider.OpenAi));

    private AttemptRunner? DecisionsRunnerInstance;

    private AttemptRunner DecisionsRunner => DecisionsRunnerInstance ??= new AttemptRunner(Logger);

    public async Task<OuroResponseBase> DecideAsync(OuroDecisionRequest request, DecisionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        options ??= new DecisionOptions();

        // Resolved before the runner, so a missing key throws to the caller instead of being caught
        // by the retry policy and returned as a failure.
        var provider = DecisionsProvider;

        Logger.LogInformation("Sending {count} decision questions to OpenAI for {promptName}",
            request.Questions?.Count ?? 0, options.PromptName ?? "(unnamed)");

        var stopwatch = Stopwatch.StartNew();

        var outcome = await DecisionsRunner.RunAsync(
            attemptToken => provider.SendAsync(request, options, attemptToken),
            OuroProvider.OpenAi,
            options.UseExponentialBackOff,
            options.Timeout ?? DecisionOptions.DefaultTimeout,
            "DecisionOptions.Timeout",
            cancellationToken);

        stopwatch.Stop();

        // The runner reports cancellation instead of throwing; this is where it becomes the .NET
        // contract, exactly as on ChatAsync.
        cancellationToken.ThrowIfCancellationRequested();

        outcome.Response.DurationMs = (int)stopwatch.ElapsedMilliseconds;

        return outcome.Response;
    }
}
