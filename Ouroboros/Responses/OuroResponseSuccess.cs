namespace Ouroboros.Responses;

public class OuroResponseSuccess : OuroResponseBase
{
    public OuroResponseSuccess(string responseText)
    {
        Success = true;
        ResponseText = responseText;
    }
}