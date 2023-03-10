using Ouroboros.LargeLanguageModels;

namespace Ouroboros.Builder;

public class PromptResponse<T>
{
    public OuroResponseBase OuroResponse { get; set; }
    public bool Success { get; set; }
    public T? Value { get; set; }


    public PromptResponse(OuroResponseBase ouroResponse)
    {
        OuroResponse = ouroResponse;
        Success = ouroResponse.Success;
        Value = default;
    }

    public PromptResponse(OuroResponseBase ouroResponse, T value)
    {
        OuroResponse = ouroResponse;
        Success = ouroResponse.Success;
        Value = value;
    }
}