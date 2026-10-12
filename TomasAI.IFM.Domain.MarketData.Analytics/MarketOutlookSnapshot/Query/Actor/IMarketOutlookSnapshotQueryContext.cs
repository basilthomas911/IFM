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
