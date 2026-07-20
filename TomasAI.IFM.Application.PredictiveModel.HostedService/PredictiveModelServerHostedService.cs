using Microsoft.Extensions.Hosting;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.PredictiveModel.Server.HostedService
{
    public class PredictiveModelServerHostedService : IHostedService
    {
        readonly IPredictiveModelServerEventConsumer _eventConsumer;

        public PredictiveModelServerHostedService(IPredictiveModelServerEventConsumer predictiveModelServerEventConsumer)
        {
            _eventConsumer = IsArgumentNull.Set(predictiveModelServerEventConsumer);
        }

        public async Task StartAsync(CancellationToken stoppingToken) => await _eventConsumer.StartAsync();
        public async Task StopAsync(CancellationToken cancellationToken) => await _eventConsumer.StopAsync();

    }
}
