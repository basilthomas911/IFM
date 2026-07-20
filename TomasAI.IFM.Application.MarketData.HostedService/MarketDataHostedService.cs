using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;

namespace TomasAI.IFM.Application.MarketData.HostedService
{
    public class MarketDataHostedService : IHostedService, IMarketDataHostedService
    {
        readonly IMarketDataApiEventConsumer _marketDataApiEventConsumer;

        public MarketDataHostedService(IMarketDataApiEventConsumer marketDataApiEventConsumer)
        {
            _marketDataApiEventConsumer = marketDataApiEventConsumer;
        }

        public async Task StartAsync(CancellationToken stoppingToken) => await _marketDataApiEventConsumer.StartAsync();
        public async Task StopAsync(CancellationToken cancellationToken) => await _marketDataApiEventConsumer.StopAsync();

    }
}
