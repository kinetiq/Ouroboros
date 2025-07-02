using Betalgo.Ranul.OpenAI.ObjectModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ouroboros.LargeLanguageModels;

internal static class ModelMappings
{
    public static string GetModelNameAsString(this OuroModels? @this, OuroModels defaultModel)
    {
        @this ??= defaultModel;

        return GetModelNameAsString(@this.Value);
    }


    /// <summary>
    /// We need to convert our generic model to a string. Turn it into a Betalgo model and use that library's
    /// capability.
    /// </summary>
    internal static string GetModelNameAsString(OuroModels ouroModel)
    {
        // Handle models that are not in Betalgo yet.
        switch (ouroModel)
        {
            case OuroModels.Gpt_o3:
                return "gpt-o3";
            case OuroModels.Gpt_o3_mini:
                return "gpt-o3-mini";
            case OuroModels.Gpt_o4_mini:
                return "gpt-o4-mini";
        }

        // This is a mapping from Ouroboros models to Betalgo models.
        var betalgoModel = ouroModel switch
        {
            OuroModels.Ada => Models.Model.Ada,
            OuroModels.Babbage => Models.Model.Babbage,
            OuroModels.Curie => Models.Model.Curie,
            OuroModels.Davinci => Models.Model.Davinci,
            OuroModels.TextAdaV1 => Models.Model.TextAdaV1,
            OuroModels.TextBabbageV1 => Models.Model.TextBabbageV1,
            OuroModels.TextCurieV1 => Models.Model.TextCurieV1,
            OuroModels.TextDavinciV1 => Models.Model.TextDavinciV1,
            OuroModels.TextDavinciV2 => Models.Model.TextDavinciV2,
            OuroModels.TextDavinciV3 => Models.Model.TextDavinciV3,
            OuroModels.Gpt3_5_Turbo => Models.Model.Gpt_3_5_Turbo,
            OuroModels.Gpt3_5_Turbo_16k => Models.Model.Gpt_3_5_Turbo_16k,
            OuroModels.Gpt_4 => Models.Model.Gpt_4,
            OuroModels.Gpt_4_32k => Models.Model.Gpt_4_32k,
            OuroModels.Gpt_4_turbo => Models.Model.Gpt_4_turbo,
            OuroModels.Gpt_4_turbo_2024_04_09 => Models.Model.Gpt_4_turbo_2024_04_09,
            OuroModels.Gpt_4_1 => Models.Model.Gpt_4_1,
            OuroModels.Gpt_4o => Models.Model.Gpt_4o,
            OuroModels.Gpt_4o_mini => Models.Model.Gpt_4o_mini,
            _ => throw new ArgumentOutOfRangeException(nameof(ouroModel), ouroModel, null)
        };

        return betalgoModel.EnumToString();
    }
}
