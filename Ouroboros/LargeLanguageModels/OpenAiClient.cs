using System.Linq;
using System.Threading.Tasks;
using OpenAI.GPT3;
using OpenAI.GPT3.Managers;
using OpenAI.GPT3.ObjectModels;
using OpenAI.GPT3.ObjectModels.RequestModels;
using OpenAI.GPT3.ObjectModels.ResponseModels;

namespace Ouroboros.LargeLanguageModels;

internal class OpenAiClient : IApiClient
{
    private readonly string ApiKey;

    public async Task<OuroResponseBase> Complete(string prompt, CompleteOptions? options)
    {
        options = options ?? new CompleteOptions();

        var api = GetClient();

        var request = MapOptions(prompt, options);

        // use polly to retry if we get Unknown Error.
        var completionResult = await api.Completions.Create(request, Models.Model.TextDavinciV3);

        if (completionResult.Successful)
        {
            var responseText = completionResult
                .Choices
                .First()
                .Text;
            
            return new OuroResponseSuccess(responseText);
        }

        var error = GetError(completionResult);

        return new OuroResponseFailure(error);
    }

    private static CompletionCreateRequest MapOptions(string prompt, CompleteOptions options)
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

    private static string GetError(CompletionCreateResponse completionResult)
    {
        return completionResult.Error == null
                   ? "Unknown Error"
                   : $"{completionResult.Error.Code}: {completionResult.Error.Message}";
    } 

    private OpenAIService GetClient()
    {
        return new OpenAIService(new OpenAiOptions
        {
            ApiKey = ApiKey
        });
    }

    internal OpenAiClient(string apiKey)
    {
        ApiKey = apiKey;
    }
}