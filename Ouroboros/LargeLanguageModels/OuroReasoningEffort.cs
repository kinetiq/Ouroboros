namespace Ouroboros.LargeLanguageModels;

/// <summary>
/// How much effort a reasoning model should spend thinking before it answers.
/// </summary>
/// <remarks>
/// Both providers express all five levels, but only their newer models accept the top two.
/// Ask an older model for XHigh or Max and the provider rejects the call, which is the same
/// contract every other unsupported combination gets here: fail where you asked for it rather
/// than quietly serve something else.
/// </remarks>
public enum OuroReasoningEffort
{
    Low,
    Medium,
    High,

    /// <summary>
    /// Between High and Max. Rejected by models older than GPT-5.6 and Claude Opus 4.7.
    /// </summary>
    XHigh,

    /// <summary>
    /// The most the model will spend. Rejected by models older than GPT-5.6 and Claude Opus 4.7.
    /// </summary>
    Max
}
