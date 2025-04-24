using Betalgo.Ranul.OpenAI.ObjectModels.ResponseModels;
using Ouroboros.Responses;
using Polly;
using System;

namespace Ouroboros.LargeLanguageModels;

/// <summary>
/// Used by ChatRequestHandler and CompletionRequestHandler.
/// </summary>
/// <typeparam name="T">An OpenAI BaseResponse</typeparam>
internal abstract class OpenAiRequestHandlerBase<T> : RequestHandlerBase<T> where T : BaseResponse
{

    protected override OuroResponseFailure HandlePolicyExhausted(PolicyResult<T> policyResult)
    {
        return policyResult switch
        {
            { FaultType: FaultType.ExceptionHandledByThisPolicy } => new OuroResponseInternalError("Exception calling endpoint: " + policyResult.FinalException!.Message),
            { FaultType: FaultType.ResultHandledByThisPolicy } => new OuroResponseOpenAiError(policyResult.FinalHandledResult!.Error),
            _ => throw new InvalidOperationException("Unhandled result type.")
        };
    }
}