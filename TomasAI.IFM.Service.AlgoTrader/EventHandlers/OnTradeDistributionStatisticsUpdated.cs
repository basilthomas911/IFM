using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.AlgoTrader.ServiceApi;

namespace TomasAI.IFM.Service.AlgoTrader.EventHandlers
{
    /// <summary>
    /// trade distribution statistics updated event handler
    /// </summary>
    public class OnTradeDistributionStatisticsUpdated : BaseEventServiceHandler,
        IAsyncEventHandler<OptionTradeDistributionStatisticsUpdatedEvent, AlgoTraderService>
    {
        private readonly ITradeStrategyEventProducer _tradeStrategyEventProducer;
        private readonly ILogger<OnTradeDistributionStatisticsUpdated> _logger;

        /// <summary>
        /// trade distribution statistics updated event handler constructor
        /// </summary>
        /// <param name="statusConsoleWriter"></param>
        /// <param name="logger"></param>
        public OnTradeDistributionStatisticsUpdated(
            ITradeStrategyEventProducer tradeStrategyEventProducer,
            IStatusConsoleWriter statusConsoleWriter,
            ILogger<OnTradeDistributionStatisticsUpdated> logger) : base(statusConsoleWriter)
        {
            _tradeStrategyEventProducer = tradeStrategyEventProducer ?? throw new ArgumentNullException(nameof(tradeStrategyEventProducer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// trade distribution statistics updated event
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public Task ExecuteAsync(OnTradeDistributionStatisticsUpdated e)
        {
            throw new NotImplementedException();
        }
    }
}
