using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query;

public static class GetFuturesEodDataByDateRange
{

    extension(GetFuturesEodDataByDateRangeQuery q)
    {
        /// <summary>Returns the typed query result from the projected read store.</summary>
        internal async ValueTask<ServiceResult<FuturesEodDataV2ReadModel[]>> ReadAsync(IDbContextFactory databases)
            => new(await q.ExecuteAsync(databases).ConfigureAwait(false));

        internal async ValueTask<FuturesEodDataV2ReadModel[]> ExecuteAsync(
        IDbContextFactory dbFactory)
            => [.. await (dbFactory.MarketDataDb.GetFuturesEodDataByDateRangeAsync(q.ContractId, q.StartDate, q.EndDate)).ConfigureAwait(false)];
    }
}
