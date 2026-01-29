using Microsoft.Extensions.DependencyInjection;
using Ouroboros.LargeLanguageModels.ChatCompletions;
using Ouroboros.LargeLanguageModels.Completions;

namespace Ouroboros.Config;

public static class UseOuroborosExtension
{
    /// <summary>
    /// Registers the Ouroboros Client and its dependencies as transient services.
    /// </summary>
    public static IServiceCollection AddOuroboros(this IServiceCollection services, string apiKey)
    {
        services.AddTransient<ChatRequestHandler>();
        services.AddTransient<CompletionRequestHandler>();

        services.AddTransient<OuroClient>(serviceProvider =>
	    {
            var chat = serviceProvider.GetService<ChatRequestHandler>();
            var completion = serviceProvider.GetService<CompletionRequestHandler>();

		    var client = new OuroClient(apiKey, completion!, chat!);

            return client;
        });
        
        return services;
    }
}