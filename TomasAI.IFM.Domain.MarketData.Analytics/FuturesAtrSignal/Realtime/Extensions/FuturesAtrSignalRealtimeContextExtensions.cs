using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Realtime.Extensions;

/// <summary>Exposes readonly FuturesAtrSignalRealtime Realtime context properties.</summary>
public static class FuturesAtrSignalRealtimeContextExtensions
{
    extension(IRealtimeActorContext<FuturesAtrSignalRealtimeActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public IFuturesAtrSignalRealtimeContext DomainContext =>
            IsArgumentNull.Set(context as IFuturesAtrSignalRealtimeContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<FuturesAtrSignalRealtimeActor> Logger => context.DomainContext.Logger;
    }
}
