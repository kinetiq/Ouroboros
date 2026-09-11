using System;

namespace Ouroboros.LargeLanguageModels;

/// <summary>
/// Turns the model enum into the id a provider's API expects.
/// </summary>
/// <remarks>
/// Public for one reason: consumers key pricing and usage tables on the provider's own model
/// string, so anything tracking cost needs to ask what a given OuroModels maps to. Reading it off a
/// response only works when there was one, which leaves failed calls with nothing to file under.
/// </remarks>
public static class ModelMappings
{
    public static string GetModelNameAsString(this OuroModels? @this, OuroModels defaultModel)
    {
        @this ??= defaultModel;

        return GetModelNameAsString(@this.Value);
    }

    /// <summary>
    /// Converts our generic model enum into the model name string the API expects.
    /// </summary>
    public static string GetModelNameAsString(OuroModels ouroModel)
    {
        // Map supported Ouroboros models to API model names.
        switch (ouroModel)
        {
            case OuroModels.Gpt_5:
                return "gpt-5";
            case OuroModels.Gpt_5_1:
                return "gpt-5.1";
            case OuroModels.Gpt_5_2:
                return "gpt-5.2";
            case OuroModels.Gpt_5_mini:
                return "gpt-5-mini";
            case OuroModels.Gpt_5_nano:
                return "gpt-5-nano";
            case OuroModels.Gpt_5_4:
                return "gpt-5.4";
            case OuroModels.Gpt_5_4_mini:
                return "gpt-5.4-mini";
            case OuroModels.Gpt_5_4_nano:
                return "gpt-5.4-nano";
            case OuroModels.Gpt_5_5:
                return "gpt-5.5";
            case OuroModels.Gpt_5_6_Sol:
                return "gpt-5.6-sol";
            case OuroModels.Gpt_5_6_Terra:
                return "gpt-5.6-terra";
            case OuroModels.Gpt_5_6_Luna:
                return "gpt-5.6-luna";
            case OuroModels.Gpt_6_Astra:
                return "gpt-6-astra";

            // Anthropic. These ids carry no date suffix - they are complete as written, and
            // appending one produces a 404.
            case OuroModels.Claude_Opus_5:
                return "claude-opus-5";
            case OuroModels.Claude_Opus_4_8:
                return "claude-opus-4-8";
            case OuroModels.Claude_Sonnet_5:
                return "claude-sonnet-5";
            case OuroModels.Claude_Haiku_4_5:
                return "claude-haiku-4-5";
            case OuroModels.Claude_Fable_5_1:
                return "claude-fable-5-1";
            case OuroModels.Claude_Fable_5:
                return "claude-fable-5";
        }

        throw new ArgumentOutOfRangeException(nameof(ouroModel), ouroModel, null);
    }
}
