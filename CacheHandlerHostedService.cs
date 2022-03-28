using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace _3ai.solutions.CacheHandler
{
    public class CacheHandlerHostedService : BackgroundService//, IAsyncDisposable
    {
        public CacheHandlerHostedService()
        {

        }

        //public Task StartAsync(CancellationToken cancellationToken = default)
        //{
        //    throw new NotImplementedException();
        //}

        //public Task StopAsync(CancellationToken cancellationToken = default)
        //{
        //    throw new NotImplementedException();
        //}

        //public void Dispose()
        //{
        //    StopAsync().Wait();
        //    GC.SuppressFinalize(this);
        //}

        //public async ValueTask DisposeAsync()
        //{
        //    await StopAsync();
        //    GC.SuppressFinalize(this);
        //}

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            throw new NotImplementedException();
        }
    }
}
