using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Realtime.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="FuturesAdxSignalRealtimeActor"/>.</summary>
public interface IFuturesAdxSignalRealtimeContext : IRealtimeActorContext<FuturesAdxSignalRealtimeActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<FuturesAdxSignalRealtimeActor> Logger { get; }
}
