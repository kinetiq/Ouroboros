using System.Threading.Tasks;
using Ouroboros.LargeLanguageModels.Completions;

namespace Ouroboros.LargeLanguageModels;

/// <summary>
/// Abstracts APIs such as OpenAI so we can easily swap these out.
/// </summary>
internal interface IApiClient
{
    Task<CompleteResponseBase> Complete(string prompt, CompleteOptions? options);
}