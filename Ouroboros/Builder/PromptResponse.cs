using Ouroboros.LargeLanguageModels.Completions;

namespace Ouroboros.Builder;

public class PromptResponse<T>
{
    public CompleteResponseBase CompleteResponse { get; set; }
    public string ResponseText { get; set; }
    public bool Success { get; set; }
    public T? Value { get; set; }


    public PromptResponse(CompleteResponseBase completeResponse)
    {
        CompleteResponse = completeResponse;
        ResponseText = CompleteResponse.ResponseText;
        Success = completeResponse.Success;
        Value = default;
    }

    public PromptResponse(CompleteResponseBase completeResponse, T value)
    {
        CompleteResponse = completeResponse;
        ResponseText = CompleteResponse.ResponseText;
        Success = completeResponse.Success;
        Value = value;
    }
}