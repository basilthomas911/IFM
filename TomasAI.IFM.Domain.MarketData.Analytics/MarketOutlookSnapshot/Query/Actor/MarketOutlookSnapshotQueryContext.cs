using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Query.Actor;

public interface IMarketOutlookSnapshotQueryContext
    : IQueryActorContext<MarketOutlookSnapshotQueryActor>
{
    IDbContextFactory DbFactory { get; }
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
        ILogger<MarketOutlookSnapshotQueryActor> logger)
        : base(supervisor, new(ActorType.Query, MarketOutlookSnapshotQueryActor.ActorName))
    {
        DbFactory = IsArgumentNull.Set(dbFactory);
        Logger = IsArgumentNull.Set(logger);
    }

    public IDbContextFactory DbFactory { get; }
    public ILogger<MarketOutlookSnapshotQueryActor> Logger { get; }
}
