using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query;

public static class GetFuturesEodDataMovingAverages
{
    internal static async ValueTask<FuturesEodDataMovingAveragesReadModel> GetFuturesEodMovingAveragesAsync(
       this GetFuturesEodDataMovingAveragesQuery q, IDbContextFactory dbFactory)
        => await dbFactory.GetFuturesEodMovingAveragesAsync(q.ContractId, q.Symbol, q.ValueDate);
}
