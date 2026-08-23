using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesMarketPrice.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesMarketPrice.Realtime.Extensions;

/// <summary>Provides typed runtime properties for <see cref="FuturesMarketPriceRealtimeActor"/> contexts.</summary>
public static class FuturesMarketPriceRealtimeRuntimeExtensions
{
    extension(IRealtimeActorContext<FuturesMarketPriceRealtimeActor> context)
    {
        /// <summary>Gets the actor supervisor.</summary>
        public IActorSupervisor Supervisor => IsArgumentNull.Set((context as IFuturesMarketPriceRealtimeContext)?.Supervisor, nameof(context))!;
        /// <summary>Gets the typed actor logger.</summary>
        public ILogger<FuturesMarketPriceRealtimeActor> Logger => IsArgumentNull.Set((context as IFuturesMarketPriceRealtimeContext)?.Logger, nameof(context))!;
    }
}

