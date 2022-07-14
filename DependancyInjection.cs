using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace _3ai.solutions.CacheHandler
{
    public static class DependancyInjection
    {
        public static IServiceCollection AddCacheHandlerServices(this IServiceCollection services, IConfiguration configuration)
        {
            var cacheHandlerOptions = configuration.GetSection("CacheHandler").Get<CacheHandlerOptions>() ?? new CacheHandlerOptions();
            services.Configure<CacheHandlerOptions>((c) => c = cacheHandlerOptions);
            services.AddSingleton<CacheHandlerService>();
            if (cacheHandlerOptions.UseHostedService)
            {
                services.AddSingleton<CacheHandlerHostedService>();
                services.AddHostedService(provider => provider.GetService<CacheHandlerHostedService>());
            }
            return services;
        }
    }
}