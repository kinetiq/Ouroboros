namespace Ouroboros.Responses;

public abstract class OuroResponseBase
{
    public bool Success { get; set; }
    public string ResponseText { get; set; } = "";
    public int TotalTokenUsage { get; set; } = 0;

    public override string ToString()
    {
        return ResponseText;
    }
}