using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query;

public static class GetFuturesEodDataByDateRange
{
    internal static async ValueTask<FuturesEodDataV2ReadModel[]> GetFuturesEodDataByDateRangeAsync(
       this GetFuturesEodDataByDateRangeQuery q, IDbContextFactory dbFactory)
        => [.. await dbFactory.MarketDataDb.GetFuturesEodDataByDateRangeAsync(q.ContractId, q.StartDate, q.EndDate)];
}
