using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace _3ai.solutions.CacheHandler
{
    public static class DependancyInjection
    {
     
        public static IServiceCollection AddCacheHandlerServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<CacheHandlerOptions>(configuration.GetSection("CacheHandler"));
            services.AddSingleton<CacheHandlerService>();
            services.AddSingleton<CacheHandlerHostedService>();
            services.AddHostedService(provider => provider.GetService<CacheHandlerHostedService>());
            return services;
        }
    }
}