using Ouroboros.LargeLanguageModels.Completions;

namespace Ouroboros.Events;

/// <summary>
/// Not implemented.
/// </summary>
public class OnRequestCompletedArgs
{
    public string Prompt { get; set; } 
    public CompleteResponseBase Response { get; set; } 
    public int Tokens { get; set; }

    public OnRequestCompletedArgs()
    {
        Prompt = "";
        Response = new CompleteResponseNoOp();
        Tokens = 0;
    }
}