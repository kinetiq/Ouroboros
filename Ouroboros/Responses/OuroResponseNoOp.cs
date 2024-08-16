namespace Ouroboros.Responses;

/// <summary>
/// Indicates that nothing was sent to the API, which could mean call was cancelled or, in the case of a dialog,
/// that the dialog was called without any sendable commands.
/// </summary>
public class OuroResponseNoOp : OuroResponseBase
{
    public override string ToString()
    {
        return $"(NoOp) Nothing was sent to the API: {ResponseText}";
    }

    public OuroResponseNoOp()
    {
        Success = true;
        ResponseText = string.Empty;
    }
}