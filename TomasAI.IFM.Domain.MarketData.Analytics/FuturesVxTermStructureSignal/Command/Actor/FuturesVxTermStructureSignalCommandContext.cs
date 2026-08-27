using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Command.Actor;

/// <summary>Defines readonly services required by the VX term-structure Command actor.</summary>
public interface IFuturesVxTermStructureSignalCommandContext
    : ICommandActorContext<FuturesVxTermStructureSignalCommandActor>
{
    ILogger<FuturesVxTermStructureSignalCommandActor> Logger { get; }
    IEventSourceActorDbContext DbEventSource { get; }
    IEventProjector<FuturesVxTermStructureSignalCommandActor> EventProjector { get; }
}

/// <summary>Provides the typed VX term-structure Command context.</summary>
public sealed class FuturesVxTermStructureSignalCommandContext : CommandActorContext,
    ICommandActorContext<FuturesVxTermStructureSignalCommandActor>,
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
