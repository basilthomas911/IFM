using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Event.Actor;

/// <summary>Provides the closed generic VWAP Event context.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesVwapSignalEventContext : EventActorContext,
IFuturesVwapSignalEventContext
{
    /// <summary>Initializes the readonly Event context.</summary>
    public FuturesVwapSignalEventContext(
        IActorSupervisor supervisor, ILogger<FuturesVwapSignalEventActor> logger)
        : base(supervisor, new(ActorType.Event, FuturesVwapSignalEventActor.ActorName)) =>
        Logger = IsArgumentNull.Set(logger);
    /// <inheritdoc />
    public ILogger<FuturesVwapSignalEventActor> Logger { get; }
}
