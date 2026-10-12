using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="FuturesTdiSignalRealtimeActor"/>.</summary>
public interface IFuturesTdiSignalRealtimeContext : IRealtimeActorContext<FuturesTdiSignalRealtimeActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the Projector service supplied to the actor context.</summary>
    IRealtimeProjector<FuturesTdiSignalRealtimeActor> Projector { get; }
    /// <summary>Gets storage used to seed restart-safe TDI cross continuity.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<FuturesTdiSignalRealtimeActor> Logger { get; }
}
