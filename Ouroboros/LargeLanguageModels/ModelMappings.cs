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
        // Map supported Ouroboros models to API model names.
        switch (ouroModel)
        {
            case OuroModels.Gpt_3_5_Turbo_Instruct:
                return "gpt-3.5-turbo-instruct";
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
        }

        throw new ArgumentOutOfRangeException(nameof(ouroModel), ouroModel, null);
    }
}
