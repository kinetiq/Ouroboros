namespace Ouroboros.Responses;

public abstract class OuroResponseBase
{
    public bool Success { get; set; }
    public string ResponseText { get; set; } = "";
    public int PromptTokens { get; set; } = 0;
    public int? CompletionTokens { get; set; } = 0;
    public int TotalTokenUsage { get; set; } = 0;

    public override string ToString()
    {
        return $"{ResponseText}";
    }
}