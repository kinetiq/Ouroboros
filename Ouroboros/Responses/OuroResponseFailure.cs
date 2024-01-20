using OpenAI.ObjectModels.ResponseModels;

namespace Ouroboros.Responses;

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

    public OuroResponseFailure(string errorDetails)
    {
        Success = false;
        ResponseText = errorDetails;
        ErrorCode = "";
        ErrorOrigin = "";
        ErrorDetails = "";
    }
}


public class OuroResponseInternalError : OuroResponseFailure
{
    public OuroResponseInternalError(string errorDetails) : base("Ouroboros internal error")
    {
        ErrorOrigin = "Ouroboros";
        ErrorDetails = errorDetails;
    }
}

public class OuroResponseOpenAiError : OuroResponseFailure
{
    public OuroResponseOpenAiError(Error? error) : base($"Error calling openAI")
    {
        ErrorOrigin = "OpenAI";
        ErrorDetails = error?.Message ?? "Unknown Error";
        ErrorCode = error?.Code ?? "";
    }
}