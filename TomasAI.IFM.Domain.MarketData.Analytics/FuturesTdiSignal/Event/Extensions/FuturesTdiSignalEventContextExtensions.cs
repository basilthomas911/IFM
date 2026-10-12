using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Event.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Event.Extensions;

/// <summary>Exposes readonly FuturesTdiSignalEvent Event context properties.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension members use their declared receiver; static conversion would remove the established actor context extension contract.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034", Justification = "C# 14 extension blocks generate nested implementation types; the public API consists of actor context extension members, not nested domain types.")]
public static class FuturesTdiSignalEventContextExtensions
{
    extension(IEventActorContext<FuturesTdiSignalEventActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public IFuturesTdiSignalEventContext DomainContext =>
            IsArgumentNull.Named(context as IFuturesTdiSignalEventContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the StatusConsoleWriter service retained by the typed context.</summary>
        public IStatusConsoleWriter StatusConsoleWriter => context.DomainContext.StatusConsoleWriter;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<FuturesTdiSignalEventActor> Logger => context.DomainContext.Logger;
    }
}
