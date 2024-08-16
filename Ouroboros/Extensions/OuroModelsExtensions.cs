using System;
using OpenAI.ObjectModels;
using Ouroboros.LargeLanguageModels;

namespace Ouroboros.Extensions;

public static class OuroModelsExtensions
{
    public static string GetModelNameAsString(this OuroModels? @this)
    {
        if (@this == null)
            throw new ArgumentNullException(nameof(@this));

        var betalgoModel = ToBetalgoModel((OuroModels)@this);

        return betalgoModel.EnumToString();
    }

    private static Models.Model ToBetalgoModel(OuroModels @this)
    {
        return @this switch
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
            OuroModels.Gpt_4 => Models.Model.Gpt_4,
            OuroModels.Gpt_4_32k => Models.Model.Gpt_4_32k,
            OuroModels.Gpt3_5_Turbo_16k => Models.Model.Gpt_3_5_Turbo_16k,
            OuroModels.Gpt_4_turbo => Models.Model.Gpt_4_turbo,
            OuroModels.Gpt_4_turbo_2024_04_09 => Models.Model.Gpt_4_turbo_2024_04_09,
            OuroModels.Gpt_4o => Models.Model.Gpt_4o,
            OuroModels.Gpt_4o_mini => Models.Model.Gpt_4o_mini,
            _ => throw new ArgumentOutOfRangeException(nameof(@this), @this, null)
        };
    }
}