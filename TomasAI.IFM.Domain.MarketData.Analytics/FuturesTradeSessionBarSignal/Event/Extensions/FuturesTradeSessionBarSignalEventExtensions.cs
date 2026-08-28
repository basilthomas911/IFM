using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Event.Extensions;

/// <summary>Exposes bar signal Event services as readonly extension properties.</summary>
public static class FuturesTradeSessionBarSignalEventExtensions
{
    extension(IEventActorContext<FuturesTradeSessionBarSignalEventActor> context)
    {
        /// <summary>Gets the typed bar signal Event context.</summary>
        public IFuturesTradeSessionBarSignalEventContext BarSignalContext =>
            context as IFuturesTradeSessionBarSignalEventContext
            ?? throw new InvalidOperationException("The bar signal Event actor requires its typed context.");
        /// <summary>Gets the server clock.</summary>
        public TimeProvider TimeProvider => context.BarSignalContext.TimeProvider;
        /// <summary>Gets the typed logger.</summary>
        public ILogger<FuturesTradeSessionBarSignalEventActor> Logger =>
            context.BarSignalContext.Logger;
    }
}
