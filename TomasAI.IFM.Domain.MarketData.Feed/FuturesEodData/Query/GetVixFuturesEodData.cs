using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query;

public static class GetVixFuturesEodData
{
    internal static async ValueTask<VixFuturesEodDataReadModel[]> ExecuteAsync(
       this GetVixFuturesEodDataQuery q, IDbContextFactory dbFactory)
    {
        var db = dbFactory.MarketDataDb;
        if (string.IsNullOrEmpty(q.ContractId))
            return [.. await (db.GetVixFuturesEodDataByValueDateAsync(q.ValueDate)).ConfigureAwait(false)];

        var result = await (db.GetVixFuturesEodDataAsync(q.ContractId, q.ValueDate)).ConfigureAwait(false);
        return result is null ? [] : [result];
    }
}
