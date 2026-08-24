using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Realtime.Extensions;

/// <summary>Exposes readonly FuturesAdxSignalRealtime Realtime context properties.</summary>
public static class FuturesAdxSignalRealtimeContextExtensions
{
    extension(IRealtimeActorContext<FuturesAdxSignalRealtimeActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public IFuturesAdxSignalRealtimeContext DomainContext =>
            IsArgumentNull.Set(context as IFuturesAdxSignalRealtimeContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the Projector service retained by the typed context.</summary>
        public IRealtimeProjector<FuturesAdxSignalRealtimeActor> Projector => context.DomainContext.Projector;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<FuturesAdxSignalRealtimeActor> Logger => context.DomainContext.Logger;
    }
}
