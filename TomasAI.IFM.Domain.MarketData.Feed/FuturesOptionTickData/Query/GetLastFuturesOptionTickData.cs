using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Query;

public static class GetLastFuturesOptionTickData
{

    extension(GetLastFuturesOptionTickDataQuery q)
    {
        /// <summary>Returns the typed query result from the projected read store.</summary>
        internal async ValueTask<ServiceResult<FuturesOptionTickDataV2ReadModel?>> ReadAsync(IDbContextFactory databases)
            => new(await q.ExecuteAsync(databases).ConfigureAwait(false));

        internal async ValueTask<FuturesOptionTickDataV2ReadModel?> ExecuteAsync(
        IDbContextFactory dbFactory)
        {
            RawTickRequestQualification.RequireScope(q.ContractId, q.ValueDate);
            return await dbFactory.MarketDataDb.GetLastFuturesOptionTickDataAsync(q.ContractId, q.ValueDate).ConfigureAwait(false);
        }
    }
}
