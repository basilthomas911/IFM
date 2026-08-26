using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Realtime.Extensions;

/// <summary>Exposes readonly FuturesMacdSignalRealtime Realtime context properties.</summary>
public static class FuturesMacdSignalRealtimeContextExtensions
{
    extension(IRealtimeActorContext<FuturesMacdSignalRealtimeActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public IFuturesMacdSignalRealtimeContext DomainContext =>
            IsArgumentNull.Set(context as IFuturesMacdSignalRealtimeContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<FuturesMacdSignalRealtimeActor> Logger => context.DomainContext.Logger;
    }
}
