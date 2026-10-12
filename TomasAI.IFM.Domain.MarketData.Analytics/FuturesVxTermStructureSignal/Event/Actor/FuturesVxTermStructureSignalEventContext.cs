using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Event.Actor;

/// <summary>Provides the typed stateless VX term-structure Event context.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesVxTermStructureSignalEventContext : EventActorContext,
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
