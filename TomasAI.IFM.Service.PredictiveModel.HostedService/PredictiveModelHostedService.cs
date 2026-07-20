using Microsoft.Extensions.Hosting;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Service.PredictiveModel.HostedService
{
    public class PredictiveModelHostedService : IHostedService, IPredictiveModelHostedService
    {
        readonly IPredictiveModelEventConsumer _predictiveModelEventConsumer;

        public PredictiveModelHostedService(IPredictiveModelEventConsumer predictiveModelEventConsumer)
        {
            _predictiveModelEventConsumer = IsArgumentNull.Set( predictiveModelEventConsumer);
        }

        public async Task StartAsync(CancellationToken stoppingToken) => await _predictiveModelEventConsumer.StartAsync();
        public async Task StopAsync(CancellationToken cancellationToken) => await _predictiveModelEventConsumer.StopAsync();
    }
}
