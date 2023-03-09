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


public class OuroResponseFailure : OuroResponseBase
{
    public OuroResponseFailure(string error)
    {
        Success = false;
        ResponseText = error;
    }
}