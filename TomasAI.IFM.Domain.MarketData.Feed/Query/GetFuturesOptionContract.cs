using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

public static class GetFuturesOptionContract
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="context"></param>
    /// <param name="p"></param>
    /// <returns></returns>
    internal static async ValueTask GetFuturesOptionContractFromBrokerAsync(
        this GetFuturesOptionContractQuery q, IQueryActorContext context, IMarketDataSnapshotApi marketDataSnapshotApi)
    {
        FuturesOptionContractReadModel futuresOptionContract;
        var streamId = 0;
        try
        {
            streamId = marketDataSnapshotApi.StreamIds.Add(q.ContractId);
            marketDataSnapshotApi.Start();
            futuresOptionContract = (await marketDataSnapshotApi.GetFuturesOptionContractAsync(streamId, q.QueryForContract!))!;
        }
        finally
        {
            marketDataSnapshotApi.StreamIds.Remove(streamId);
            marketDataSnapshotApi.Stop();
        }
        await context.ReplyAsync(q.Subject.ThreadId, GetFuturesOptionContractQuery.Verb, new ServiceResult<FuturesOptionContractReadModel>(futuresOptionContract!));
    }

}
