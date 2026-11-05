using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Query;

/// <summary>Handles one durable Market Outlook snapshot query and its typed reply.</summary>
public static class GetMarketOutlookSnapshot
{

    extension(GetMarketOutlookSnapshotQuery query)
    {
        /// <summary>Reads the requested snapshot and replies using the existing query result contract.</summary>
        public async ValueTask<ServiceResult<MarketOutlookReadModel>> ExecuteAsync(
            IQueryActorContext<MarketOutlookSnapshotQueryActor> context,
            IMarketOutlookSnapshotQueryContext domainContext,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            var result = await domainContext.GetMarketOutlookSnapshotAsync(
                query.ContractId, query.ValueDate, cancellationToken).ConfigureAwait(false);
            return result;
        }
    }
}
