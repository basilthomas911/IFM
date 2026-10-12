using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Extensions;

/// <summary>Exposes readonly FuturesTdiSignalRealtime Realtime context properties.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension members use their declared receiver; static conversion would remove the established actor context extension contract.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034", Justification = "C# 14 extension blocks generate nested implementation types; the public API consists of actor context extension members, not nested domain types.")]
public static class FuturesTdiSignalRealtimeContextExtensions
{
    extension(IRealtimeActorContext<FuturesTdiSignalRealtimeActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public IFuturesTdiSignalRealtimeContext DomainContext =>
            IsArgumentNull.Named(context as IFuturesTdiSignalRealtimeContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the Projector service retained by the typed context.</summary>
        public IRealtimeProjector<FuturesTdiSignalRealtimeActor> Projector => context.DomainContext.Projector;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<FuturesTdiSignalRealtimeActor> Logger => context.DomainContext.Logger;
    }
}
