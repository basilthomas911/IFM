using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;

namespace TomasAI.IFM.Service.MarketDataFeed.HostedService
{
    public class MarketDataFeedHostedService : IHostedService, IMarketDataFeedHostedService
    {
        readonly IMarketDataFeedEventConsumer _marketDataFeedEventConsumer;

        public MarketDataFeedHostedService(IMarketDataFeedEventConsumer marketDataFeedEventConsumer)
        {
            _marketDataFeedEventConsumer = marketDataFeedEventConsumer;
        }

        public async Task StartAsync(CancellationToken stoppingToken) => await _marketDataFeedEventConsumer.StartAsync();
        public async Task StopAsync(CancellationToken cancellationToken) => await _marketDataFeedEventConsumer.StopAsync();

    }
}
