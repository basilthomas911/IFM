using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Extensions;

/// <summary>Provides typed runtime properties for <see cref="TickAggregationRealtimeActor"/> contexts.</summary>
public static class TickAggregationRealtimeRuntimeExtensions
{
    extension(IRealtimeActorContext<TickAggregationRealtimeActor> context)
    {
        /// <summary>Gets the actor supervisor.</summary>
        public IActorSupervisor Supervisor => IsArgumentNull.Set((context as ITickAggregationRealtimeContext)?.Supervisor, nameof(context))!;
        /// <summary>Gets the typed actor logger.</summary>
        public ILogger<TickAggregationRealtimeActor> Logger => IsArgumentNull.Set((context as ITickAggregationRealtimeContext)?.Logger, nameof(context))!;
    }
}

