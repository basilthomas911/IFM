using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Service.TradePosition.HostedService
{
    public class TradePositionHostedService : IHostedService
    {
        private readonly ITradePositionEventConsumer _tradePositionEventConsumer;
        private readonly ILogger<TradePositionHostedService> _logger;

        public TradePositionHostedService(
            ITradePositionEventConsumer tradePositionEventConsumer,
            ILogger<TradePositionHostedService> logger)
        {
            _tradePositionEventConsumer = tradePositionEventConsumer;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken stoppingToken)
        {
            try
            {
                await _tradePositionEventConsumer.StartAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Trade-position event consumer startup was cancelled by API shutdown.");
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Trade-position event consumer failed to start; the API host will remain running.");
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _tradePositionEventConsumer.StopAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Trade-position event consumer reached the API shutdown deadline.");
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Trade-position event consumer failed while stopping; API shutdown will continue.");
            }
        }

    }
}
