namespace Ouroboros.LargeLanguageModels;

/// <summary>
/// How much effort a reasoning model should spend thinking before it answers.
/// </summary>
/// <remarks>
/// Deliberately limited to the three levels every supported provider can express today.
/// Appending members later is a non-breaking change.
/// </remarks>
public enum OuroReasoningEffort
{
    Low,
    Medium,
    High
}
