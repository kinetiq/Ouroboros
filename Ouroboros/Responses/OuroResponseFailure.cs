using Betalgo.Ranul.OpenAI.ObjectModels.ResponseModels;

namespace Ouroboros.Responses;

/// <summary>
/// Generic failure response. This is often extended to make it easier to work with. See OuroResponseInternalError and
/// OuroResponseOpenAiError for examples.
/// </summary>
public class OuroResponseFailure : OuroResponseBase
{
    /// <summary>
    /// What system or component caused the error, for example OpenAI.
    /// </summary>
    public string ErrorOrigin { get; set; }

    /// <summary>
    /// ErrorCode, if available.
    /// </summary>
    public string ErrorCode { get; set; }

    /// <summary>
    /// Long-form error details, if available.
    /// </summary>
    public string ErrorDetails { get; set; }

    public override string ToString()
    {
        return $"({ErrorOrigin}) {ResponseText}: {ErrorCode} {ErrorDetails}";
    }

    public OuroResponseFailure(string errorDetails)
    {
        Success = false;
        ResponseText = errorDetails;
        ErrorCode = "";
        ErrorOrigin = "";
        ErrorDetails = "";
    }
}

/// <summary>
/// Indicates an error occurred within Ouroboros itself, which is to say not OpenAI or in some endpoint code.
/// </summary>
public class OuroResponseInternalError : OuroResponseFailure
{
    public OuroResponseInternalError(string errorDetails) : base("Ouroboros Internal Error")
    {
        ErrorOrigin = "Ouroboros";
        ErrorDetails = errorDetails;
    }
}

/// <summary>
/// Indicates an error occurred within OpenAI.
/// </summary>
public class OuroResponseOpenAiError : OuroResponseFailure
{
    public OuroResponseOpenAiError(Error? error) : base($"Error Calling OpenAI")
    {
        ErrorOrigin = "OpenAI";
        ErrorDetails = error?.Message ?? "Unknown Error";
        ErrorCode = error?.Code ?? "";
    }
}