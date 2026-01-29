using System;
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
        return AddOuroboros(services, apiKey, null);
    }

    /// <summary>
    /// Registers the Ouroboros Client and its dependencies as transient services.
    /// Allows configuration of the client (e.g., setting up OnChatCompleted hook).
    /// </summary>
    public static IServiceCollection AddOuroboros(this IServiceCollection services, string apiKey, Action<OuroClient, IServiceProvider>? configure)
    {
        services.AddTransient<ChatRequestHandler>();
        services.AddTransient<CompletionRequestHandler>();

        services.AddTransient<OuroClient>(serviceProvider =>
        {
            var chat = serviceProvider.GetService<ChatRequestHandler>();
            var completion = serviceProvider.GetService<CompletionRequestHandler>();

            var client = new OuroClient(apiKey, completion!, chat!);

            configure?.Invoke(client, serviceProvider);

            return client;
        });
        
        return services;
    }
}