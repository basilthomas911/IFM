using System;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.TradePlan.HostedService
{
    /// <summary>
    /// Consumes trade-plan events from NATS actor subjects and forwards them to the trade-plan service.
    /// </summary>
    public class TradePlanEventConsumer : NatsEventConsumer, ITradePlanEventConsumer
    {
        private readonly ITradePlanService _tradePlanService;
        private readonly Guid _siteId;

        public TradePlanEventConsumer(
            ITradePlanService tradePlanService,
            INatsEventListenerOptions options,
            ILogger<TradePlanEventConsumer> logger,
            NatsConnectionManager? connectionManager = null)
            : base(options, logger, connectionManager)
        {
            _tradePlanService = tradePlanService;
            _siteId = Guid.NewGuid();
        }

        protected override void ConnectEvents() 
            => Subscribe($"{_siteId}", 
                new IEvent[] { new TradePlanUpdatedEvent { }.SetEventSource($"{EventTopic.TradeEvents}"),
                               new TradePlanForwardLossLimitWarningUpdatedEvent { }.SetEventSource($"{EventTopic.TradeEvents}"),
                               new TradePlanForwardLossLimitReachedUpdatedEvent { }.SetEventSource($"{EventTopic.TradeEvents}"),
                               new TradePlanForwardLossLimitClearedEvent { }.SetEventSource($"{EventTopic.TradeEvents}") } , 
                async e => await _tradePlanService.ExecuteAsync(e));
    }
}
