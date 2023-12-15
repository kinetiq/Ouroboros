using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Endpoints;

namespace Ouroboros.Config;

public static class UseOuroborosExtension
{

    /// <summary>
    /// Registers the Ouroboros Client as a transient service.
    /// </summary>
    public static IServiceCollection AddOuroboros(this IServiceCollection services, string apiKey, ITemplateEndpoint? endpoint = null)
    {
	    services.AddTransient<OuroClient>(serviceProvider =>
	    {
		    var resolvedEndpoint = endpoint ?? serviceProvider.GetService<ITemplateEndpoint>();
		    return new OuroClient(apiKey, endpoint);
	    });
        
        return services;
    }
}