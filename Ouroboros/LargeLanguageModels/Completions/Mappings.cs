using System;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;

namespace Ouroboros.LargeLanguageModels.Completions;

internal class Mappings
{
    private const Models.Model DefaultBetalgoModel = Models.Model.TextDavinciV3;

    /// <summary>
    /// Maps our generic options to OpenAI options.
    /// </summary>
    internal static CompletionCreateRequest MapOptions(string prompt, CompleteOptions options)
    {
        return new CompletionCreateRequest
        {
            Prompt = prompt,
            BestOf = options.BestOf,
            Temperature = options.Temperature,
            TopP = options.TopP,
            FrequencyPenalty = options.FrequencyPenalty,
            PresencePenalty = options.PresencePenalty,
            MaxTokens = options.MaxTokens,
            LogitBias = options.LogitBias,
            N = 1,
            Stop = options.Stop,
            StopAsList = options.StopAsList,
            User = options.User,
            Echo = false,
            Suffix = options.Suffix,
            Model = GetModelNameAsString(options.Model)
        };
    }

    /// <summary>
    /// We need to convert our generic model to a string. Turn it into a Betalgo model and use that library's
    /// capability.
    /// </summary>
    internal static string GetModelNameAsString(OuroModels? ouroModel)
    {
        var betalgoModel = ouroModel switch
        {
            null => DefaultBetalgoModel,
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
            OuroModels.TextEmbeddingAdaV2 => Models.Model.TextEmbeddingAdaV2,
            OuroModels.ChatGpt3_5Turbo => Models.Model.ChatGpt3_5Turbo,
            OuroModels.Gpt_4 => Models.Model.Gpt_4,
            _ => throw new ArgumentOutOfRangeException(nameof(ouroModel), ouroModel, null)
        };

        return betalgoModel.EnumToString();
    }
}