using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query;

public static class GetFuturesEodDataMovingAverages
{

    extension(GetFuturesEodDataMovingAveragesQuery q)
    {
        /// <summary>Returns the typed query result from the projected read store.</summary>
        internal async ValueTask<ServiceResult<FuturesEodDataMovingAveragesReadModel>> ReadAsync(IDbContextFactory databases)
            => new(await q.ExecuteAsync(databases).ConfigureAwait(false));

        internal async ValueTask<FuturesEodDataMovingAveragesReadModel> ExecuteAsync(
        IDbContextFactory dbFactory)
            => await (dbFactory.GetFuturesEodMovingAveragesAsync(q.ContractId, q.Symbol, q.ValueDate)).ConfigureAwait(false);
    }
}
