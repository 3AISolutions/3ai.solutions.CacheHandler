using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace _3ai.solutions.CacheHandler
{
    public class CacheHandlerHostedService : BackgroundService
    {
        private readonly CacheHandlerService _cacheHandlerService;
        private readonly int _waitTimeMilliseconds;

        public CacheHandlerHostedService(CacheHandlerService cacheHandlerService, IOptions<CacheHandlerOptions> option)
        {
            _cacheHandlerService = cacheHandlerService;
            _waitTimeMilliseconds = option.Value.BackgroundWaitTimeMilliseconds;
        }

        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                string? item = _cacheHandlerService.GetCacheKeyToReset();
                while (!string.IsNullOrEmpty(item))
                {
                    try
                    {
                        await _cacheHandlerService.Reset(item);
                        item = _cacheHandlerService.GetCacheKeyToReset();
                    }
                    catch
                    {
                        //notify something? also time and 
                    }
                }

                await Task.Delay(_waitTimeMilliseconds, stoppingToken);
            }
        }
    }
}
