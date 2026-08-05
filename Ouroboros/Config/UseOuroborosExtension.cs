using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ouroboros.LargeLanguageModels.ChatCompletions;

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

        services.AddTransient<OuroClient>(serviceProvider =>
        {
            var chat = serviceProvider.GetService<ChatRequestHandler>();

            // GetService, not GetRequiredService: a host without logging configured should still
            // get a working client. HookFailurePolicy.Log then falls back to NullLogger.
            var logger = serviceProvider.GetService<ILogger<OuroClient>>();

            var client = new OuroClient(apiKey, chat!, logger);

            configure?.Invoke(client, serviceProvider);

            return client;
        });

        // Registered against the interface too, so consumers can depend on IOuroClient.
        // Resolves through the registration above rather than building a second client.
        services.AddTransient<IOuroClient>(serviceProvider => serviceProvider.GetRequiredService<OuroClient>());

        return services;
    }
}