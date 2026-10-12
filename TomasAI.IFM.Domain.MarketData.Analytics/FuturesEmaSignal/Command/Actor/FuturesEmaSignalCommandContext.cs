using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.Actor;

/// <summary>Provides the typed EMA command context.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesEmaSignalCommandContext : CommandActorContext,
IFuturesEmaSignalCommandContext
{
    /// <summary>Initializes the context.</summary>
    public FuturesEmaSignalCommandContext(IActorSupervisor supervisor,
        IEventSourceActorDbContext dbEventSource,
        IEventProjector<FuturesEmaSignalCommandActor> eventProjector,
        ILogger<FuturesEmaSignalCommandActor> logger)
        : base(supervisor, new(ActorType.Command, FuturesEmaSignalCommandActor.ActorName))
    {
        DbEventSource = IsArgumentNull.Set(dbEventSource);
        EventProjector = IsArgumentNull.Set(eventProjector);
        Logger = IsArgumentNull.Set(logger);
    }
    /// <inheritdoc />
    public IEventSourceActorDbContext DbEventSource { get; }
    /// <inheritdoc />
    public IEventProjector<FuturesEmaSignalCommandActor> EventProjector { get; }
    /// <summary>Gets the actor logger.</summary>
    public ILogger<FuturesEmaSignalCommandActor> Logger { get; }
}
