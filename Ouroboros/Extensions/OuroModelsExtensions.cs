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
            //OuroModels.TextEmbeddingAdaV2 => Models.Model.TextEmbeddingAdaV2,
            OuroModels.Gpt3_5_Turbo => Models.Model.Gpt_3_5_Turbo,
            OuroModels.Gpt_4 => Models.Model.Gpt_4,
            _ => throw new ArgumentOutOfRangeException(nameof(@this), @this, null)
        };
    }
}