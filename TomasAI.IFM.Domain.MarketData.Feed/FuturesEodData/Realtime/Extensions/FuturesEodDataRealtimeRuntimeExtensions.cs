using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Extensions;

/// <summary>Provides typed runtime properties for <see cref="FuturesEodDataRealtimeActor"/> contexts.</summary>
public static class FuturesEodDataRealtimeRuntimeExtensions
{
    extension(IRealtimeActorContext<FuturesEodDataRealtimeActor> context)
    {
        /// <summary>Gets the actor supervisor.</summary>
        public IActorSupervisor Supervisor => IsArgumentNull.Set((context as IFuturesEodDataRealtimeContext)?.Supervisor, nameof(context))!;
        /// <summary>Gets the typed actor logger.</summary>
        public ILogger<FuturesEodDataRealtimeActor> Logger => IsArgumentNull.Set((context as IFuturesEodDataRealtimeContext)?.Logger, nameof(context))!;
    }
}

