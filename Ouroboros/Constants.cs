using Ouroboros.LargeLanguageModels;

namespace Ouroboros;

public static class Constants
{
    public const OuroModels DefaultChatModel = OuroModels.Gpt_5_4_mini;

    /// <summary>
    /// Reasoning effort to use when running in default chat mode; this only takes effect
    /// when the default chat model is being used.
    /// </summary>
    public static readonly OuroReasoningEffort? DefaultReasoningEffort = null;
}
