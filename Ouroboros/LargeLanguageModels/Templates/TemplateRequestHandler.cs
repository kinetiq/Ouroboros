using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ouroboros.Chaining.TemplateDialog.Templates;
using Ouroboros.Endpoints;
using Ouroboros.LargeLanguageModels.Resilience;
using Ouroboros.Responses;
using Polly;
using Z.Core.Extensions;

namespace Ouroboros.LargeLanguageModels.Templates;

internal class TemplateRequestHandler
{
    private readonly IServiceProvider Services;

    /// <summary>
    /// Executes a template.
    /// </summary>
    public async Task<OuroResponseBase> SendTemplateAsync(IOuroTemplateBase template, ITemplateEndpoint endpoint, TemplateOptions? options = null)
    {
        options ??= new TemplateOptions();

        // TODO: there's no way to pass in our options currently.
        
        var delay = BackoffPolicy.GetBackoffPolicy(options.UseExponentialBackOff);

        // OpenAI errors: https://platform.openai.com/docs/guides/error-codes/api-errors

        var policyResult = await Policy
            .Handle<Exception>()
            .OrResult<OuroResponseBase>(response => 
                    (response is OuroResponseOpenAiError error && error.ErrorCode.In("", "429", "500", "503")) || // "" means nothing was given to us... Might as well retry.
                    (response is OuroResponseFailure { ErrorOrigin: "Python" }) 
                ) 
            .WaitAndRetryAsync(
                sleepDurations: delay,
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    var logger = Services.GetService<ILogger<TemplateRequestHandler>>();

                    if (logger == null)
                        return;

                    logger?.LogWarning("Delaying for {delay}ms, then attempting retry {retry}.", timespan.TotalMilliseconds, retryAttempt);
                })
            .ExecuteAndCaptureAsync(async () => await endpoint.SendTemplateAsync(template));

        // Handle Success
        if (policyResult.Outcome == OutcomeType.Successful)
        {
            var response = policyResult.Result;

            if (response == null)
                return new OuroResponseInternalError("While retrying, PolicyResult was successful, however the inner result was null. This should never happen.");

            return response;
        }

        // Handle Failure
        return policyResult switch
        {
            { FaultType: FaultType.ExceptionHandledByThisPolicy } => new OuroResponseInternalError("Exception calling endpoint: " + policyResult.FinalException!.Message),
            { FaultType: FaultType.ResultHandledByThisPolicy } => policyResult.Result,
        _ => throw new InvalidOperationException("Unhandled FaultType: " + policyResult.FaultType)
        };
    }

    public TemplateRequestHandler(IServiceProvider services)
    {
        Services = services;
    }
}
