using Ouroboros.LargeLanguageModels;
using System;

namespace Ouroboros;

public static class Constants
{
    /// <summary>
    /// The model used when a call names none.
    /// </summary>
    /// <remarks>
    /// static readonly rather than const deliberately. A const is baked into every assembly that
    /// references it at compile time, so changing this would leave consumers on the old value until
    /// they happened to rebuild - and the version number would give no hint that anything moved.
    /// </remarks>
    public static readonly OuroModels DefaultChatModel = OuroModels.Gpt_5_4_mini;

    /// <summary>
    /// Reasoning effort to use when running in default chat mode; this only takes effect
    /// when the default chat model is being used.
    /// </summary>
    public static readonly OuroReasoningEffort? DefaultReasoningEffort = null;

    /// <summary>
    /// How long one attempt may run when ChatOptions.Timeout is not set.
    /// </summary>
    /// <remarks>
    /// Deliberately generous. The model decides how long it takes, and there is no useful upper
    /// bound short of one: reasoning models routinely run past a conventional HTTP default, and
    /// provider-side code execution can run for minutes.
    /// </remarks>
    public static readonly TimeSpan DefaultAttemptTimeout = TimeSpan.FromMinutes(10);
}
