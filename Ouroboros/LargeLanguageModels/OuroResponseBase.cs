namespace Ouroboros.LargeLanguageModels;

public abstract class OuroResponseBase
{
    public bool Success { get; set; }
    public string ResponseText { get; set; } = "";

    public override string ToString()
    {
        return ResponseText;
    }
}

public class OuroResponseSuccess : OuroResponseBase
{
    public OuroResponseSuccess(string responseText)
    {
        Success = true;
        ResponseText = responseText;
    }
}


/// <summary>
/// Indicates that the API call was cancelled because there was nothing to do.
/// </summary>
public class OuroResponseNoOp : OuroResponseBase
{
    public OuroResponseNoOp()
    {
        Success = true;
        ResponseText = string.Empty;
    }
}


public class OuroResponseFailure : OuroResponseBase
{
    public OuroResponseFailure(string error)
    {
        Success = false;
        ResponseText = error;
    }
}