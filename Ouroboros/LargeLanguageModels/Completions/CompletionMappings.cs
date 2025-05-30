using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;

namespace Ouroboros.LargeLanguageModels.Completions;

internal class CompletionMappings
{
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
            Model = options.Model.GetModelNameAsString(Constants.DefaultCompletionModel) 
        };
    }
}