using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Query;

public static class GetLastFuturesOptionTickData
{
    internal static async ValueTask<FuturesOptionTickDataV2ReadModel?> GetLastFuturesOptionTickDataAsync(
       this GetLastFuturesOptionTickDataQuery q, IDbContextFactory dbFactory)
        => await dbFactory.MarketDataDb.GetLastFuturesOptionTickDataAsync(q.ContractId, q.ValueDate);
}
