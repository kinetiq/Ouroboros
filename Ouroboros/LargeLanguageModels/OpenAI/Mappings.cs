using System;
using OpenAI.GPT3.ObjectModels;
using OpenAI.GPT3.ObjectModels.RequestModels;

namespace Ouroboros.LargeLanguageModels.OpenAI;

internal class Mappings
{
    /// <summary>
    /// Maps our generic options to OpenAI options.
    /// </summary>
    internal static CompletionCreateRequest MapOptions(string prompt, CompleteOptions options)
    {
        var request = new CompletionCreateRequest
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
            Suffix = options.Suffix
        };
        return request;
    }

    /// <summary>
    /// Convert our generic model list into one suitable for this library.
    /// </summary>
    internal static Models.Model MapModel(OuroModels ouroModel)
    {
        return ouroModel switch
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
            OuroModels.ChatGpt3_5Turbo => Models.Model.ChatGpt3_5Turbo,
            OuroModels.Gpt_4 => Models.Model.Gpt_4,
            _ => throw new ArgumentOutOfRangeException(nameof(ouroModel), ouroModel, null)
        };
    }
}