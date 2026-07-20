using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace TomasAI.IFM.Service.TradePosition.HostedService
{
    public class TradePositionHostedService : IHostedService
    {
        private readonly ITradePositionEventConsumer _tradePositionEventConsumer;

        public TradePositionHostedService(ITradePositionEventConsumer tradePositionEventConsumer)
        {
            _tradePositionEventConsumer = tradePositionEventConsumer;
        }

        public async Task StartAsync(CancellationToken stoppingToken) => await _tradePositionEventConsumer.StartAsync();
        public async Task StopAsync(CancellationToken cancellationToken) => await _tradePositionEventConsumer.StopAsync();

    }
}
