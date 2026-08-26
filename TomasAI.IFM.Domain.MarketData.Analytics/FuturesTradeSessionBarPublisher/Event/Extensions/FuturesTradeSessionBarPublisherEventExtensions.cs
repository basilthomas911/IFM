using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Event.Extensions;

/// <summary>Exposes bar publisher Event services as readonly extension properties.</summary>
public static class FuturesTradeSessionBarPublisherEventExtensions
{
    extension(IEventActorContext<FuturesTradeSessionBarPublisherEventActor> context)
    {
        /// <summary>Gets the typed bar publisher Event context.</summary>
        public IFuturesTradeSessionBarPublisherEventContext BarPublisherContext =>
            context as IFuturesTradeSessionBarPublisherEventContext
            ?? throw new InvalidOperationException("The bar publisher Event actor requires its typed context.");
        /// <summary>Gets the server clock.</summary>
        public TimeProvider TimeProvider => context.BarPublisherContext.TimeProvider;
        /// <summary>Gets the typed logger.</summary>
        public ILogger<FuturesTradeSessionBarPublisherEventActor> Logger =>
            context.BarPublisherContext.Logger;
    }
}
