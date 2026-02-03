using Ouroboros.LargeLanguageModels;

namespace Ouroboros.Responses;

public abstract class OuroResponseBase
{
    public bool Success { get; set; }
    public string ResponseText { get; set; } = "";
    public int PromptTokens { get; set; } = 0;
    public int? CompletionTokens { get; set; } = 0;
    public int TotalTokenUsage { get; set; } = 0;

    /// <summary>
    /// Duration of the API call in milliseconds.
    /// </summary>
    public int DurationMs { get; set; }

    public override string ToString()
    {
        return $"{ResponseText}";
    }
}