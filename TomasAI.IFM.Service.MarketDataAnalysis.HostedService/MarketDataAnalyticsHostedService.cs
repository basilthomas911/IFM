using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using TomasAI.IFM.Shared.MarketDataAnalytics.ServiceApi;

namespace TomasAI.IFM.Service.MarketDataAnalytics.HostedService
{
    public class MarketDataAnalyticsHostedService : IHostedService, IMarketDataAnalyticsHostedService
    {
        readonly IMarketDataAnalyticsEventConsumer _marketDataAnalyticsEventConsumer;

        public MarketDataAnalyticsHostedService(IMarketDataAnalyticsEventConsumer marketDataAnalyticsEventConsumer)
        {
            _marketDataAnalyticsEventConsumer = marketDataAnalyticsEventConsumer;
        }

        public async Task StartAsync(CancellationToken stoppingToken) => await _marketDataAnalyticsEventConsumer.StartAsync();
        public async Task StopAsync(CancellationToken cancellationToken) => await _marketDataAnalyticsEventConsumer.StopAsync();
    }
}
