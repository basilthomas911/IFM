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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Optional legacy consumer startup reports exceptions without bringing down the API host.")]
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _tradePositionEventConsumer.StartAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
            {
                LifecycleLog.Log1(_logger, exception);
            }
            catch (Exception exception)
            {
                LifecycleLog.Log2(_logger, exception);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Optional consumer stop reports errors while allowing API shutdown to continue.")]
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _tradePositionEventConsumer.StopAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
            {
                LifecycleLog.Log3(_logger, exception);
            }
            catch (Exception exception)
            {
                LifecycleLog.Log4(_logger, exception);
            }
        }

    }
}
