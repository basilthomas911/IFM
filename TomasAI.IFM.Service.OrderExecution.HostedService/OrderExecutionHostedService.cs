using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace TomasAI.IFM.Service.OrderExecution.HostedService
{
    public class OrderExecutionHostedService : IHostedService
    {
        private readonly IOrderExecutionEventConsumer _orderExecutionEventConsumer;

        public OrderExecutionHostedService(IOrderExecutionEventConsumer orderExecutionEventConsumer)
        {
            _orderExecutionEventConsumer = orderExecutionEventConsumer;
        }

        public async Task StartAsync(CancellationToken stoppingToken) => await _orderExecutionEventConsumer.StartAsync();
        public async Task StopAsync(CancellationToken cancellationToken) => await _orderExecutionEventConsumer.StopAsync();

    }
}
