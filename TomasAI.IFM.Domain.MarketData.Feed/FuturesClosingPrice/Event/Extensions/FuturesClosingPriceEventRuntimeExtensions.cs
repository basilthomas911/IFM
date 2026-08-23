using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Event.Extensions;

/// <summary>Provides typed runtime properties for <see cref="FuturesClosingPriceEventActor"/> contexts.</summary>
public static class FuturesClosingPriceEventRuntimeExtensions
{
    extension(IEventActorContext<FuturesClosingPriceEventActor> context)
    {
        /// <summary>Gets the actor supervisor.</summary>
        public IActorSupervisor Supervisor => IsArgumentNull.Set((context as IFuturesClosingPriceEventContext)?.Supervisor, nameof(context))!;
        /// <summary>Gets the typed actor logger.</summary>
        public ILogger<FuturesClosingPriceEventActor> Logger => IsArgumentNull.Set((context as IFuturesClosingPriceEventContext)?.Logger, nameof(context))!;
    }
}

