using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Query.Actor;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class MarketOutlookSnapshotQueryContext
    : QueryActorContext,
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
