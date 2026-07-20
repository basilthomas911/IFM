using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace TomasAI.IFM.Application.Event.Denormalizer.HostedService
{
    /// <summary>
    /// event denormalizer hosted service
    /// </summary>
    public class EventDenormalizerHostedService : IHostedService
    {
        private readonly IFundEventDenormalizerConsumer _fundEventConsumer;
        private readonly IMarketDataEventDenormalizerConsumer _marketDataEventConsumer;
        private readonly IMarketDataFeedEventDenormalizerConsumer _marketDataFeedEventConsumer;
        private readonly IOptionPricerEventDenormalizerConsumer _optionPricerEventConsumer;
        private readonly IReferenceEventDenormalizerConsumer _referenceEventConsumer;
        private readonly ITradeEventDenormalizerConsumer _tradeEventConsumer;
        private readonly ITradeOrderEventDenormalizerConsumer _tradeOrderEventConsumer;
        private readonly IExceptionEventDenormalizerConsumer _exceptionEventConsumer;

        /// <summary>
        /// event denormalizer hosted service constructor
        /// </summary>
        /// <param name="fundEventConsumer"></param>
        /// <param name="marketDataEventConsumer"></param>
        /// <param name="marketDataFeedEventConsumer"></param>
        /// <param name="optionPricerEventConsumer"></param>
        /// <param name="referenceEventConsumer"></param>
        /// <param name="tradeEventConsumer"></param>
        /// <param name="exceptionEventConsumer"></param>
        public EventDenormalizerHostedService(
            IFundEventDenormalizerConsumer fundEventConsumer,
            IMarketDataEventDenormalizerConsumer marketDataEventConsumer,
            IMarketDataFeedEventDenormalizerConsumer marketDataFeedEventConsumer,
            IOptionPricerEventDenormalizerConsumer optionPricerEventConsumer,
            IReferenceEventDenormalizerConsumer referenceEventConsumer,
            ITradeEventDenormalizerConsumer tradeEventConsumer,
            ITradeOrderEventDenormalizerConsumer tradeOrderEventConsumer,
            IExceptionEventDenormalizerConsumer exceptionEventConsumer)
        {
            _fundEventConsumer = fundEventConsumer;
            _marketDataEventConsumer = marketDataEventConsumer;
            _marketDataFeedEventConsumer = marketDataFeedEventConsumer;
            _optionPricerEventConsumer = optionPricerEventConsumer;
            _referenceEventConsumer = referenceEventConsumer;
            _tradeEventConsumer = tradeEventConsumer;
            _tradeOrderEventConsumer = tradeOrderEventConsumer;
            _exceptionEventConsumer = exceptionEventConsumer;
        }

        /// <summary>
        /// started hosted services
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        public async Task StartAsync(CancellationToken stoppingToken)
        {
            await _fundEventConsumer.StartAsync();
            await _marketDataEventConsumer.StartAsync();
            await _marketDataFeedEventConsumer.StartAsync();
            await _optionPricerEventConsumer.StartAsync();
            await _referenceEventConsumer.StartAsync();
            await _tradeEventConsumer.StartAsync();
            await _tradeOrderEventConsumer.StartAsync();
            await _exceptionEventConsumer.StartAsync();
        }

        /// <summary>
        /// stop hosted services
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _fundEventConsumer.StopAsync();
            await _marketDataEventConsumer.StopAsync();
            await _marketDataFeedEventConsumer.StopAsync();
            await _optionPricerEventConsumer.StopAsync();
            await _referenceEventConsumer.StopAsync();
            await _tradeEventConsumer.StopAsync();
            await _tradeOrderEventConsumer.StopAsync();
            await _exceptionEventConsumer.StopAsync();
        }

    }
}
