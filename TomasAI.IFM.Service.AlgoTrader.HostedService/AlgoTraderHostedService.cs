using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace TomasAI.IFM.Service.AlgoTrader.HostedService
{
    public class AlgoTraderHostedService : IHostedService
    {
        private readonly IAlgoTraderEventConsumer _algoTraderEventConsumer;

        public AlgoTraderHostedService(IAlgoTraderEventConsumer algoTraderEventConsumer)
        {
            _algoTraderEventConsumer = algoTraderEventConsumer;
        }

        public async Task StartAsync(CancellationToken stoppingToken) => await _algoTraderEventConsumer.StartAsync();
        public async Task StopAsync(CancellationToken cancellationToken) => await _algoTraderEventConsumer.StopAsync();

    }
}
