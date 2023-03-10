using Ouroboros.LargeLanguageModels;

namespace Ouroboros.Builder;

internal class ChainedCommand
{
    public string? Text { get; set; }
    public string? NewElementName { get; set; }
    public CompleteOptions? Options { get; set; }
}