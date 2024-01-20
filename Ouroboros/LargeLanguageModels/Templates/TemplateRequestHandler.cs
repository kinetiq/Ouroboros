using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI.Managers;
using OpenAI.ObjectModels.ResponseModels;
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
    /// Executes a call to OpenAI using the ChatGPT API.
    /// </summary>
    public async Task<OuroResponseBase> SendTemplateAsync(IOuroTemplateBase template, ITemplateEndpoint endpoint, OpenAIService api, TemplateOptions? options = null)
    {
        options ??= new TemplateOptions();

        // TODO: ok, here we have to write code to call our endpoint.

        return await endpoint.SendTemplateAsync(template);

    }

    /// <summary>
    /// Extracts the ResponseText from a completion response we already know to be
    /// successful.
    /// </summary>
    private static OuroResponseSuccess GetSuccessResponse(ChatCompletionCreateResponse response)
    {
        if (!response.Successful)
            throw new InvalidOperationException("Called GetResponseText on a response that was not marked successful. This should never happen.");

        var responseText = response.Choices
            .First()
            .Message
            .Content!
            .Trim();

        return new OuroResponseSuccess(responseText)
        {
            Model = response.Model,
            PromptTokens = response.Usage.PromptTokens,
            CompletionTokens = response.Usage.CompletionTokens,
            TotalTokenUsage = response.Usage.TotalTokens
        };
    }

    public TemplateRequestHandler(IServiceProvider services)
    {
        Services = services;
    }
}
