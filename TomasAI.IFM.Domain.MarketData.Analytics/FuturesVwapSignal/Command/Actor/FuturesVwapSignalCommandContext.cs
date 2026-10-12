using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.Actor;

/// <summary>Provides the closed generic VWAP Command context.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class FuturesVwapSignalCommandContext : CommandActorContext,
IFuturesVwapSignalCommandContext
{
    /// <summary>Initializes the readonly Command context.</summary>
    public FuturesVwapSignalCommandContext(
        IActorSupervisor supervisor,
        IEventSourceActorDbContext dbEventSource,
        IEventSourceActorStateRepository<FuturesVwapSignalCommandState> stateRepository,
        IEventProjector<FuturesVwapSignalCommandActor> eventProjector,
        ILogger<FuturesVwapSignalCommandActor> logger)
        : base(supervisor, new(ActorType.Command, FuturesVwapSignalCommandActor.ActorName))
    {
        DbEventSource = IsArgumentNull.Set(dbEventSource);
        StateRepository = IsArgumentNull.Set(stateRepository);
        EventProjector = IsArgumentNull.Set(eventProjector);
        Logger = IsArgumentNull.Set(logger);
    }
    /// <inheritdoc />
    public IEventSourceActorDbContext DbEventSource { get; }
    /// <inheritdoc />
    public IEventSourceActorStateRepository<FuturesVwapSignalCommandState> StateRepository { get; }
    /// <inheritdoc />
    public IEventProjector<FuturesVwapSignalCommandActor> EventProjector { get; }
    /// <inheritdoc />
    public ILogger<FuturesVwapSignalCommandActor> Logger { get; }
}
