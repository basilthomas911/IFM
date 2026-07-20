using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace TomasAI.IFM.Service.ErrorConsole.HostedService
{
    public class ErrorConsoleHostedService : IHostedService
    {
        private readonly IErrorConsoleEventConsumer _errorConsoleEventConsumer;

        public ErrorConsoleHostedService(IErrorConsoleEventConsumer errorConsoleEventConsumer)
        {
            _errorConsoleEventConsumer = errorConsoleEventConsumer;
        }

        public async Task StartAsync(CancellationToken stoppingToken) => await _errorConsoleEventConsumer.StartAsync();
        public async Task StopAsync(CancellationToken cancellationToken) => await _errorConsoleEventConsumer.StopAsync();

    }
}
