using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OpenAI.GPT3;
using OpenAI.GPT3.Managers;
using OpenAI.GPT3.ObjectModels;
using OpenAI.GPT3.ObjectModels.RequestModels;

namespace Ouroboros.OpenAI;

internal class Gpt3Client 
{
    public async Task<string> Complete(string text)
    {
        var api = GetClient();

        var request = new CompletionCreateRequest
        {
            Prompt = text,
            Temperature = .7f,
            TopP = 1,
            FrequencyPenalty = .5f,
            PresencePenalty = 0,
            MaxTokens = 256
        };

        var completionResult = await api.Completions.Create(request, Models.Model.TextDavinciV2);

        if (completionResult.Successful)
            return completionResult.Choices.First()
                                   .Text;

        return completionResult.Error == null
                   ? "Unknown Error"
                   : $"{completionResult.Error.Code}: {completionResult.Error.Message}";
    }

    private OpenAIService GetClient()
    {
        return new OpenAIService(new OpenAiOptions
        {
            ApiKey = "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"
        });
    }
}