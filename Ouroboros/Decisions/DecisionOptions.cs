using System;
using System.Diagnostics.CodeAnalysis;

namespace Ouroboros.Decisions;

/// <summary>
/// Per-call settings for a decision request.
/// </summary>
/// <remarks>
/// Separate from ChatOptions on purpose. Most chat settings have no meaning here, and a shared type
/// would accept them and ignore them.
/// </remarks>
[Experimental(DecisionsDiagnostics.ExperimentalId)]
public sealed class DecisionOptions
{
    /// <summary>
    /// The model used when <see cref="Model" /> is not changed.
    /// </summary>
    /// <remarks>
    /// static readonly rather than const, for the reason given on Constants.DefaultChatModel.
    /// </remarks>
    public static readonly string DefaultModel = "gpt-6-luna";

    /// <summary>
    /// How long one attempt may run when <see cref="Timeout" /> is not set.
    /// </summary>
    /// <remarks>
    /// Much shorter than the chat default of ten minutes. A decision call is expected to take well
    /// under a second, so an attempt still running after this is stuck, not thinking.
    /// </remarks>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The OpenAI model id to send.
    /// </summary>
    /// <remarks>
    /// A string rather than OuroModels. The Decisions models cannot chat, so an OuroModels value for
    /// one would let ChatAsync accept a model that fails every call.
    /// </remarks>
    public string Model { get; set; } = DefaultModel;

    /// <summary>
    /// How long a single attempt may run before it is abandoned. Null uses <see cref="DefaultTimeout" />.
    /// </summary>
    /// <remarks>
    /// A per-attempt budget, as on ChatOptions.Timeout: each retry gets a fresh one. To bound the
    /// whole call, pass a CancellationToken.
    /// </remarks>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Retries rate limits, server faults and network faults with exponential backoff. On by default.
    /// </summary>
    public bool UseExponentialBackOff { get; set; } = true;

    /// <summary>
    /// A name for this request, used in logs.
    /// </summary>
    public string? PromptName { get; set; }
}
