using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace TomasAI.IFM.Application.OptionPricer.HostedService
{
    public class OptionPricerHostedService : IHostedService
    {
        private readonly IOptionPricerEventConsumer _optionPricerEventConsumer;

        public OptionPricerHostedService(IOptionPricerEventConsumer optionPricerEventConsumer)
        {
            _optionPricerEventConsumer = optionPricerEventConsumer;
        }

        public async Task StartAsync(CancellationToken stoppingToken) => await _optionPricerEventConsumer.StartAsync();
        public async Task StopAsync(CancellationToken cancellationToken) => await _optionPricerEventConsumer.StopAsync();

    }
}
