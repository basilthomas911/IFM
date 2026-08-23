using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Realtime.Extensions;

/// <summary>Provides typed runtime properties for <see cref="FuturesOptionTickDataRealtimeActor"/> contexts.</summary>
public static class FuturesOptionTickDataRealtimeRuntimeExtensions
{
    extension(IRealtimeActorContext<FuturesOptionTickDataRealtimeActor> context)
    {
        /// <summary>Gets the actor supervisor.</summary>
        public IActorSupervisor Supervisor => IsArgumentNull.Set((context as IFuturesOptionTickDataRealtimeContext)?.Supervisor, nameof(context))!;
        /// <summary>Gets the typed actor logger.</summary>
        public ILogger<FuturesOptionTickDataRealtimeActor> Logger => IsArgumentNull.Set((context as IFuturesOptionTickDataRealtimeContext)?.Logger, nameof(context))!;
    }
}

