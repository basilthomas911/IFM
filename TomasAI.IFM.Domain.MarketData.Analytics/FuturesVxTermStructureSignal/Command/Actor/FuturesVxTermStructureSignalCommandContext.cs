using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Command.Actor;

/// <summary>Provides the typed VX term-structure Command context.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesVxTermStructureSignalCommandContext : CommandActorContext,
    IFuturesVxTermStructureSignalCommandContext
{
    /// <summary>Initializes the typed context.</summary>
    public FuturesVxTermStructureSignalCommandContext(
        IActorSupervisor supervisor,
        IEventSourceActorDbContext dbEventSource,
        IEventProjector<FuturesVxTermStructureSignalCommandActor> eventProjector,
        ILogger<FuturesVxTermStructureSignalCommandActor> logger)
        : base(supervisor, new(ActorType.Command, FuturesVxTermStructureSignalCommandActor.ActorName))
    {
        DbEventSource = IsArgumentNull.Set(dbEventSource);
        EventProjector = IsArgumentNull.Set(eventProjector);
        Logger = IsArgumentNull.Set(logger);
    }
    /// <inheritdoc />
    public IEventSourceActorDbContext DbEventSource { get; }
    /// <inheritdoc />
    public IEventProjector<FuturesVxTermStructureSignalCommandActor> EventProjector { get; }
    /// <inheritdoc />
    public ILogger<FuturesVxTermStructureSignalCommandActor> Logger { get; }
}
