using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.Actor;

public interface IMarketOutlookSnapshotCommandContext
    : ICommandActorContext<MarketOutlookSnapshotCommandActor>
{
    IEventSourceActorStateRepository<MarketOutlookSnapshotCommandState> Repository { get; }
    IEventProjector<MarketOutlookSnapshotCommandActor> EventProjector { get; }
    ILogger<MarketOutlookSnapshotCommandActor> Logger { get; }
}

public sealed class MarketOutlookSnapshotCommandContext
    : CommandActorContext,
      ICommandActorContext<MarketOutlookSnapshotCommandActor>,
      IMarketOutlookSnapshotCommandContext
{
    public MarketOutlookSnapshotCommandContext(
        IActorSupervisor supervisor,
        IEventSourceActorStateRepository<MarketOutlookSnapshotCommandState> repository,
        IEventProjector<MarketOutlookSnapshotCommandActor> eventProjector,
        ILogger<MarketOutlookSnapshotCommandActor> logger)
        : base(supervisor, new(ActorType.Command, MarketOutlookSnapshotCommandActor.ActorName))
    {
        Repository = IsArgumentNull.Set(repository);
        EventProjector = IsArgumentNull.Set(eventProjector);
        Logger = IsArgumentNull.Set(logger);
    }

    public IEventSourceActorStateRepository<MarketOutlookSnapshotCommandState> Repository { get; }
    public IEventProjector<MarketOutlookSnapshotCommandActor> EventProjector { get; }
    public ILogger<MarketOutlookSnapshotCommandActor> Logger { get; }
}
