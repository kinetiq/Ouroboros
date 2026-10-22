using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ouroboros.Decisions;

namespace Ouroboros.Config;

public static class UseOuroborosExtension
{
    /// <summary>
    /// Registers the Ouroboros Client and its dependencies as transient services, for OpenAI only.
    /// </summary>
    public static IServiceCollection AddOuroboros(this IServiceCollection services, string apiKey)
    {
        return AddOuroboros(services, apiKey, null);
    }

    /// <summary>
    /// Registers the Ouroboros Client and its dependencies as transient services, for OpenAI only.
    /// Allows configuration of the client (e.g., setting up OnChatCompleted hook).
    /// </summary>
    public static IServiceCollection AddOuroboros(this IServiceCollection services, string apiKey,
        Action<OuroClient, IServiceProvider>? configure)
    {
        return AddOuroboros(services, options => options.OpenAiApiKey = apiKey, configure);
    }

    /// <summary>
    /// Registers the Ouroboros Client with credentials for one or more providers.
    /// </summary>
    /// <remarks>
    /// Use this overload to reach Claude models. Only the providers you actually use need a key -
    /// requesting a model whose provider has none configured fails with a message naming it.
    /// </remarks>
    /// <example>
    /// services.AddOuroboros(options =>
    /// {
    ///     options.OpenAiApiKey = configuration["OpenAI:ApiKey"];
    ///     options.AnthropicApiKey = configuration["Anthropic:ApiKey"];
    /// });
    /// </example>
    public static IServiceCollection AddOuroboros(this IServiceCollection services,
        Action<OuroborosOptions> configureOptions, Action<OuroClient, IServiceProvider>? configure = null)
    {
        if (configureOptions is null)
            throw new ArgumentNullException(nameof(configureOptions));

        var options = new OuroborosOptions();
        configureOptions(options);

        // Registration time, which is host startup. The client itself is transient, so validating
        // only in its constructor would defer this to the first chat - and a fallback chain that
        // cannot work is exactly the thing you want to hear about before serving traffic.
        options.Validate();

        services.AddTransient<OuroClient>(serviceProvider =>
        {
            // GetService, not GetRequiredService: a host without logging configured should still
            // get a working client. HookFailurePolicy.Log then falls back to NullLogger.
            var logger = serviceProvider.GetService<ILogger<OuroClient>>();

            var client = new OuroClient(options, logger);

            configure?.Invoke(client, serviceProvider);

            return client;
        });

        // Registered against the interface too, so consumers can depend on IOuroClient.
        // Resolves through the registration above rather than building a second client.
        services.AddTransient<IOuroClient>(serviceProvider => serviceProvider.GetRequiredService<OuroClient>());

        // Same client again. Registering the experimental interface does not reach consumers:
        // OURO001 fires only where code names IOuroDecisions, so a host that never asks for it
        // never sees the diagnostic.
#pragma warning disable OURO001
        services.AddTransient<IOuroDecisions>(serviceProvider => serviceProvider.GetRequiredService<OuroClient>());
#pragma warning restore OURO001

        return services;
    }
}
