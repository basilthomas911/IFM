using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Realtime.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Realtime.Extensions;

/// <summary>Exposes readonly FuturesAdxSignalRealtime Realtime context properties.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension members use their declared receiver; static conversion would remove the established actor context extension contract.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034", Justification = "C# 14 extension blocks generate nested implementation types; the public API consists of actor context extension members, not nested domain types.")]
public static class FuturesAdxSignalRealtimeContextExtensions
{
    extension(IRealtimeActorContext<FuturesAdxSignalRealtimeActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public IFuturesAdxSignalRealtimeContext DomainContext =>
            IsArgumentNull.Named(context as IFuturesAdxSignalRealtimeContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<FuturesAdxSignalRealtimeActor> Logger => context.DomainContext.Logger;
    }
}
