using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query;

public static class GetVixFuturesEodData
{
    internal static async ValueTask<VixFuturesEodDataReadModel[]> GetVixFuturesEodDataAsync(
       this GetVixFuturesEodDataQuery q, IDbContextFactory dbFactory)
    {
        var db = dbFactory.MarketDataDb;
        if (string.IsNullOrEmpty(q.ContractId))
            return [.. await db.GetVixFuturesEodDataByValueDateAsync(q.ValueDate)];

        var result = await db.GetVixFuturesEodDataAsync(q.ContractId, q.ValueDate);
        return result is null ? [] : [result];
    }
}
