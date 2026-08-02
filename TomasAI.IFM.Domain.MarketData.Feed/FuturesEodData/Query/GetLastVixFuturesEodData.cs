using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query;

public static class GetLastVixFuturesEodData
{
    internal static async ValueTask<VixFuturesEodDataReadModel?> GetLastVixFuturesEodDataAsync(
       this GetLastVixFuturesEodDataQuery q, IDbContextFactory dbFactory)
        => await dbFactory.MarketDataDb.GetLastVixFuturesEodDataAsync(q.ContractId, q.ValueDate);
}
