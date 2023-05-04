using System.Collections.Generic;
using System.Threading.Tasks;
using Ouroboros.LargeLanguageModels.Completions;
using Ouroboros.LargeLanguageModels.Embeddings;

namespace Ouroboros.LargeLanguageModels;

/// <summary>
/// Abstracts APIs such as OpenAI so we can easily swap these out.
/// </summary>
internal interface IApiClient
{
    Task<CompleteResponseBase> Complete(string prompt, CompleteOptions? options);
    Task<EmbeddingResponseBase> RequestEmbeddings(List<string> inputs, OuroModels? model);
    Task<EmbeddingResponseBase> RequestEmbeddings(string input, OuroModels? model = null);
}