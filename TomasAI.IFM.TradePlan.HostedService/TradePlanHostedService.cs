using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace TomasAI.IFM.TradePlan.HostedService
{
    public class TradePlanHostedService : IHostedService
    {
        private readonly ITradePlanEventConsumer _tradePlanEventConsumer;

        public TradePlanHostedService(ITradePlanEventConsumer tradePositionEventConsumer)
        {
            _tradePlanEventConsumer = tradePositionEventConsumer;
        }

        public async Task StartAsync(CancellationToken stoppingToken) => await _tradePlanEventConsumer.StartAsync();
        public async Task StopAsync(CancellationToken cancellationToken) => await _tradePlanEventConsumer.StopAsync();

    }
}
