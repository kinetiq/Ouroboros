namespace Ouroboros.Responses;

/// <summary>
/// Generic failure response. This is often extended to make it easier to work with. See OuroResponseInternalError and
/// OuroResponseProviderError for examples.
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
    public OuroResponseInternalError(string errorDetails) : base(errorDetails)
    {
        ErrorOrigin = "Ouroboros";
    }
}

/// <summary>
/// Indicates the model provider returned an error.
/// </summary>
/// <remarks>
/// The constructor is internal: consumers only ever receive this type and pattern-match on it,
/// they never construct one. That keeps us free to reshape it as providers are added.
/// </remarks>
public class OuroResponseProviderError : OuroResponseFailure
{
    internal OuroResponseProviderError(string origin, string? code, string? message)
        : base($"Error calling {origin}")
    {
        ErrorOrigin = origin;
        ErrorDetails = message ?? "Unknown Error";
        ErrorCode = code ?? "";
    }
}