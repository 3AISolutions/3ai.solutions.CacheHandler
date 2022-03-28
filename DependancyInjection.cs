using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace _3ai.solutions.CacheHandler
{
    public static class DependancyInjection
    {
     
        public static IServiceCollection AddConfigurationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<CacheSettings>(configuration.GetSection("CacheSettings"));
            services.AddSingleton<CacheHandlerService>();
            services.AddSingleton<CacheHandlerHostedService>();
            services.AddHostedService(provider => provider.GetService<CacheHandlerHostedService>());
            return services;
        }
    }
}