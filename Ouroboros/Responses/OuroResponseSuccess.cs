namespace Ouroboros.Responses;

public class OuroResponseSuccess : OuroResponseBase
{
    public string Model { get; set; } = "";

    /// <summary>
    /// Used with Structured Outputs. If you specify a ResponseType in the options, this will be the
    /// deserialized result. 
    /// </summary>
    public object? ResponseObject { get; set; }
    
    public OuroResponseSuccess(string responseText)
    {
        Success = true;
        ResponseText = responseText;
        ResponseObject = null;
    }
}