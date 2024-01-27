using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Endpoints;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.LargeLanguageModels.Completions;
using Ouroboros.LargeLanguageModels.Templates;

namespace Ouroboros.Config;

public static class UseOuroborosExtension
{
    /// <summary>
    /// Registers the Ouroboros Client and its dependencies as transient services.
    /// </summary>
    public static IServiceCollection AddOuroboros(this IServiceCollection services, string apiKey, ITemplateEndpoint? endpoint = null)
    {
        services.AddTransient<ChatRequestHandler>();
        services.AddTransient<CompletionRequestHandler>();
        services.AddTransient<TemplateRequestHandler>();

        services.AddTransient<OuroClient>(serviceProvider =>
	    {
            var chat = serviceProvider.GetService<ChatRequestHandler>();
            var completion = serviceProvider.GetService<CompletionRequestHandler>();
            var template = serviceProvider.GetService<TemplateRequestHandler>();

		    var client = new OuroClient(apiKey, completion!, chat!, template!);

            // If a template endpoint is provided, set it up. Otherwise, leave this alone and
            // devs can provide it via SendTemplateAsync or manually on the client.
            var resolvedEndpoint = endpoint ?? serviceProvider.GetService<ITemplateEndpoint>();

            if (resolvedEndpoint != null)
                client.SetTemplateEndpoint(resolvedEndpoint);

            return client;
        });
        
        return services;
    }
}