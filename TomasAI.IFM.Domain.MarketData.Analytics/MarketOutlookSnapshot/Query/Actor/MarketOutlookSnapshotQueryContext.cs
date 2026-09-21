using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Query.Actor;

public interface IMarketOutlookSnapshotQueryContext
    : IQueryActorContext<MarketOutlookSnapshotQueryActor>
{
    IDbContextFactory DbFactory { get; }
    IHistoricalObservationStore HistoricalObservationStore { get; }
    MarketOutlookSnapshotQueryPolicy Policy { get; }
    ILogger<MarketOutlookSnapshotQueryActor> Logger { get; }
}

public sealed class MarketOutlookSnapshotQueryContext
    : QueryActorContext,
      IQueryActorContext<MarketOutlookSnapshotQueryActor>,
      IMarketOutlookSnapshotQueryContext
{
    public MarketOutlookSnapshotQueryContext(
        IActorSupervisor supervisor,
        IDbContextFactory dbFactory,
        IHistoricalObservationStore historicalObservationStore,
        ILogger<MarketOutlookSnapshotQueryActor> logger,
        MarketOutlookSnapshotQueryPolicy? policy = null)
        : base(supervisor, new(ActorType.Query, MarketOutlookSnapshotQueryActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        HistoricalObservationStore = IsArgumentNull.Set(historicalObservationStore);
        Logger = IsArgumentNull.Set(logger);
        Policy = policy ?? MarketOutlookSnapshotQueryPolicy.AllowAll;
    }

    public IDbContextFactory DbFactory { get; }
    public IHistoricalObservationStore HistoricalObservationStore { get; }
    public MarketOutlookSnapshotQueryPolicy Policy { get; }
    public ILogger<MarketOutlookSnapshotQueryActor> Logger { get; }
}
