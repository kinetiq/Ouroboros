namespace Ouroboros.Responses;

public class OuroResponseSuccess : OuroResponseBase
{
    public string Model { get; set; } = "";

    public OuroResponseSuccess(string responseText)
    {
        Success = true;
        ResponseText = responseText;
    }
}