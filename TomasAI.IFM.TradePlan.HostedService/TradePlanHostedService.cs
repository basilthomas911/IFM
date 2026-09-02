using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.TradePlan.HostedService
{
    public class TradePlanHostedService : IHostedService
    {
        private readonly ITradePlanEventConsumer _tradePlanEventConsumer;
        private readonly ILogger<TradePlanHostedService> _logger;

        public TradePlanHostedService(
            ITradePlanEventConsumer tradePositionEventConsumer,
            ILogger<TradePlanHostedService> logger)
        {
            _tradePlanEventConsumer = tradePositionEventConsumer;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken stoppingToken)
        {
            try
            {
                await _tradePlanEventConsumer.StartAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Trade-plan event consumer startup was cancelled by API shutdown.");
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Trade-plan event consumer failed to start; the API host will remain running.");
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _tradePlanEventConsumer.StopAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Trade-plan event consumer reached the API shutdown deadline.");
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Trade-plan event consumer failed while stopping; API shutdown will continue.");
            }
        }

    }
}
