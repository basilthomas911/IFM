using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;

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
    internal static async ValueTask<FuturesOptionContractReadModel> GetFuturesOptionContractFromBrokerAsync(
        this GetFuturesOptionContractQuery q, IMarketDataSnapshotApi marketDataSnapshotApi)
        => await GetFuturesOptionContractFromBrokerAsync(
            marketDataSnapshotApi, q.ContractId, q.QueryForContract!);

    internal static async ValueTask<FuturesOptionContractReadModel> GetFuturesOptionContractFromBrokerAsync(
        IMarketDataSnapshotApi marketDataSnapshotApi,
        string contractId,
        FuturesOptionContractReadModel queryForContract)
    {
        FuturesOptionContractReadModel futuresOptionContract;
        var streamId = 0;
        try
        {
            streamId = marketDataSnapshotApi.StreamIds.Add(contractId);
            await marketDataSnapshotApi.StartAsync().ConfigureAwait(false);
            futuresOptionContract = (await marketDataSnapshotApi.GetFuturesOptionContractAsync(
                streamId, queryForContract))!;
        }
        finally
        {
            marketDataSnapshotApi.StreamIds.Remove(streamId);
            marketDataSnapshotApi.Stop();
        }
        return futuresOptionContract;
    }

}
