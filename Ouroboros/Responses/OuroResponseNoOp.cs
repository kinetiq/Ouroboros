namespace Ouroboros.Responses;

/// <summary>
/// Indicates that the API call was cancelled because there was nothing to do.
/// </summary>
public class OuroResponseNoOp : OuroResponseBase
{
    public override string ToString()
    {
        return $"(NoOp) API call was cancelled: {ResponseText}";
    }

    public OuroResponseNoOp()
    {
        Success = true;
        ResponseText = string.Empty;
    }
}