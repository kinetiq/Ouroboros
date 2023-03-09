using Microsoft.Extensions.DependencyInjection;

namespace Ouroboros.Config;

public static class UseOuroborosExtension
{

    /// <summary>
    /// Registers the Ouroboros Client as a transient service.
    /// </summary>
    public static IServiceCollection AddOuroboros(this IServiceCollection services, string apiKey)
    {
        services.AddTransient<OuroClient>(x => new OuroClient(apiKey));

        return services;
    }
}