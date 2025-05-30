using System;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.Responses;
using Polly;

namespace Ouroboros.LargeLanguageModels;

/// <summary>
/// The most abstract base class for all request handlers.
/// </summary>
internal abstract class RequestHandlerBase<T>
{
    protected OuroResponseBase HandleResponse(PolicyResult<T> policyResult, Type responseType)
    {
        if (policyResult.Outcome == OutcomeType.Successful)
        {
            T response = policyResult.Result;

            if (response == null)
                return new OuroResponseInternalError("PolicyResult was successful, however the inner result was null. This should never happen.");

            return HandlePolicySatisfied(response, responseType);
        }

        return HandlePolicyExhausted(policyResult);
    }

    protected abstract OuroResponseBase HandlePolicySatisfied(T response, Type responseType);

    protected abstract OuroResponseFailure HandlePolicyExhausted(PolicyResult<T> policyResult);
}