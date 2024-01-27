using Ouroboros.Responses;
using Polly;

namespace Ouroboros.LargeLanguageModels;

/// <summary>
/// The most abstract base class for all request handlers.
/// </summary>
internal abstract class RequestHandlerBase<T>
{
    protected OuroResponseBase HandleResponse(PolicyResult<T> policyResult)
    {
        if (policyResult.Outcome == OutcomeType.Successful)
        {
            T response = policyResult.Result;

            if (response == null)
                return new OuroResponseInternalError("PolicyResult was successful, however the inner result was null. This should never happen.");

            return HandlePolicySatisfied(response);
        }

        return HandlePolicyExhausted(policyResult);
    }

    protected abstract OuroResponseBase HandlePolicySatisfied(T response);

    protected abstract OuroResponseFailure HandlePolicyExhausted(PolicyResult<T> policyResult);
}