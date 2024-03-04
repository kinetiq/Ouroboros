using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Chaining.TemplateDialog.Templates;
using Ouroboros.Endpoints;
using Ouroboros.Extensions;
using Ouroboros.LargeLanguageModels.Resilience;
using Ouroboros.Responses;
using Polly;

namespace Ouroboros.LargeLanguageModels.Templates;

/// <summary>
/// Handles sending templates to a custom endpoint. In order for retry to work, the endpoint must send back error codes.
/// </summary>
internal class TemplateRequestHandler : RequestHandlerBase<OuroResponseBase>
{
    private readonly ILogger<TemplateRequestHandler> Logger;

    /// <summary>
    /// Executes a template.
    /// </summary>
    public async Task<OuroResponseBase> SendTemplateAsync(IOuroTemplateBase template, ITemplateEndpoint endpoint)
    {
        var delay = BackoffPolicy.GetBackoffPolicy(endpoint.UseExponentialBackOff);

        // OpenAI errors: https://platform.openai.com/docs/guides/error-codes/api-errors

        Logger.LogInformation(
            "Sending template {template} to endpoint {endpoint} with UseExponentialBackoff = {useBackoff}",
            template.PromptName, endpoint.GetType().Name, endpoint.UseExponentialBackOff);

        var policyResult = await Policy
            .Handle<Exception>()
            .OrResult<OuroResponseBase>(response =>
                    response is OuroResponseOpenAiError error &&
                    error.ErrorCode.In("", "429", "500",
                        "503") // "" means nothing was given to us... Might as well retry.
            )
            .WaitAndRetryAsync(
                delay,
                (outcome, timespan, retryAttempt, context) =>
                {
                    Logger.LogWarning("Delaying for {delay}ms, then attempting retry {retry}.",
                        timespan.TotalMilliseconds, retryAttempt);
                })
            .ExecuteAndCaptureAsync(() => endpoint.SendTemplateAsync(template));

        return HandleResponse(policyResult);
    }

    /// <summary>
    /// Because endpoints return an OuroResponse, when a template policy is satisfied, we can just return.
    /// </summary>
    protected override OuroResponseBase HandlePolicySatisfied(OuroResponseBase response)
    {
        return response;
    }

    protected override OuroResponseFailure HandlePolicyExhausted(PolicyResult<OuroResponseBase> policyResult)
    {
        return policyResult switch
        {
            { FaultType: FaultType.ExceptionHandledByThisPolicy } => new OuroResponseInternalError(
                "Exception calling endpoint: " + policyResult.FinalException!.Message),
            { FaultType: FaultType.ResultHandledByThisPolicy } => (OuroResponseFailure)policyResult.Result,
            _ => throw new InvalidOperationException("Unhandled FaultType: " + policyResult.FaultType)
        };
    }

    public TemplateRequestHandler(ILogger<TemplateRequestHandler>? logger)
    {
        Logger = logger ?? NullLogger<TemplateRequestHandler>.Instance;
    }
}