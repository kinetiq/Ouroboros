namespace Ouroboros.LargeLanguageModels.Completions;

public abstract class CompleteResponseBase
{
    public bool Success { get; set; }
    public string ResponseText { get; set; } = "";
    public int TotalTokenUsage { get; set; } = 0;

    public override string ToString()
    {
        return ResponseText;
    }
}

public class CompleteResponseSuccess : CompleteResponseBase
{
    public CompleteResponseSuccess(string responseText)
    {
        Success = true;
        ResponseText = responseText;
    }
}


/// <summary>
/// Indicates that the API call was cancelled because there was nothing to do.
/// </summary>
public class CompleteResponseNoOp : CompleteResponseBase
{
    public CompleteResponseNoOp()
    {
        Success = true;
        ResponseText = string.Empty;
    }
}


public class CompleteResponseFailure : CompleteResponseBase
{
    public CompleteResponseFailure(string error)
    {
        Success = false;
        ResponseText = error;
    }
}