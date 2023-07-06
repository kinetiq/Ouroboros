namespace Ouroboros.Responses;

public class OuroResponseFailure : OuroResponseBase
{
    public OuroResponseFailure(string error)
    {
        Success = false;
        ResponseText = error;
    }
}