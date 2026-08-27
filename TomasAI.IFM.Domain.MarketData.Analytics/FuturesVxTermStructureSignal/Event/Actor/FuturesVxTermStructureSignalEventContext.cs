using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Event.Actor;

/// <summary>Defines readonly services required by the VX term-structure Event actor.</summary>
public interface IFuturesVxTermStructureSignalEventContext
    : IEventActorContext<FuturesVxTermStructureSignalEventActor>
{
    ILogger<FuturesVxTermStructureSignalEventActor> Logger { get; }
}

/// <summary>Provides the typed stateless VX term-structure Event context.</summary>
public sealed class FuturesVxTermStructureSignalEventContext : EventActorContext,
    IEventActorContext<FuturesVxTermStructureSignalEventActor>,
    IFuturesVxTermStructureSignalEventContext
{
    /// <summary>Initializes the typed context.</summary>
    public FuturesVxTermStructureSignalEventContext(
        IActorSupervisor supervisor,
        ILogger<FuturesVxTermStructureSignalEventActor> logger)
        : base(supervisor, new(ActorType.Event, FuturesVxTermStructureSignalEventActor.ActorName)) =>
        Logger = IsArgumentNull.Set(logger);
    /// <inheritdoc />
    public ILogger<FuturesVxTermStructureSignalEventActor> Logger { get; }
}
