using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Realtime.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Realtime.Extensions;

/// <summary>Exposes readonly FuturesRsiSignalRealtime Realtime context properties.</summary>
public static class FuturesRsiSignalRealtimeContextExtensions
{
    extension(IRealtimeActorContext<FuturesRsiSignalRealtimeActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public IFuturesRsiSignalRealtimeContext DomainContext =>
            IsArgumentNull.Set(context as IFuturesRsiSignalRealtimeContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<FuturesRsiSignalRealtimeActor> Logger => context.DomainContext.Logger;
    }
}
