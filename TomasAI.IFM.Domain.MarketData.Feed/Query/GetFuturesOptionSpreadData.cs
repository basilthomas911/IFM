using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

public static class GetFuturesOptionSpreadData
{
    internal static async ValueTask GetFuturesOptionSpreadDataAsync(
        this GetFuturesOptionSpreadDataQuery q, IQueryActorContext context, IMarketDataSnapshotApi marketDataSnapshotApi)
    {
        var shortRequestId = 0;
        var longRequestId = 0;
        FuturesOptionSpreadDataReadModel spreadData = default!;
        try
        {
            marketDataSnapshotApi.Start();
            var (shortContract, longContract) = await marketDataSnapshotApi.GetFuturesOptionSpreadAsync(q.QueryForShortOptionContract, q.QueryForLongOptionContract);
            if (shortContract == null || longContract == null)
            {
                marketDataSnapshotApi.Stop();
                throw new InvalidOperationException("MarketDataFeedQueryState.GetFuturesOptionSpreadDataAsync: Unknown futures option contract definition(s)");
            }
            var shortOption = default(FuturesOptionTickDataV2ReadModel);
            shortRequestId = marketDataSnapshotApi.StreamIds.Add(shortContract.ContractId);
            await marketDataSnapshotApi.GetFuturesOptionPriceAsync(shortRequestId, q.ValueDate, shortContract, e => shortOption = e);
            if (shortOption == null)
            {
                marketDataSnapshotApi.Stop();
                throw new InvalidOperationException($"MarketDataFeedQueryState.GetFuturesOptionSpreadDataAsync: Unknown short futures option contract definition '{shortContract.ContractId}'");
            }
            var shortOptionGreeks = marketDataSnapshotApi.GetFuturesOptionGreeks(q.ValueDate, q.MaturityDate, shortContract, shortOption.OptionPrice, q.AssetPrice, q.RiskFreeRate);

            var longOption = default(FuturesOptionTickDataV2ReadModel);
            longRequestId = marketDataSnapshotApi.StreamIds.Add(longContract.ContractId);
            await marketDataSnapshotApi.GetFuturesOptionPriceAsync(longRequestId, q.ValueDate, longContract, e => longOption = e);
            if (longOption == null)
            {
                marketDataSnapshotApi.Stop();
                throw new InvalidOperationException($"MarketDataFeedQueryState.GetFuturesOptionSpreadDataAsync: Unknown long futures option contract definition '{longContract.ContractId}'");
            }
            var longOptionGreeks = marketDataSnapshotApi.GetFuturesOptionGreeks(q.ValueDate, q.MaturityDate, longContract, longOption.OptionPrice, q.AssetPrice, q.RiskFreeRate);
            spreadData = new(
                shortLeg: new(
                    bidPrice: shortOption.BidPrice,
                    askPrice: shortOption.AskPrice,
                    impliedVolatility: shortOptionGreeks?.ImpliedVolatility ?? 0.0,
                    delta: shortOptionGreeks?.Delta ?? 0.0,
                    gamma: shortOptionGreeks?.Gamma ?? 0.0,
                    theta: shortOptionGreeks?.Theta ?? 0.0),
                longLeg: new(
                    bidPrice: longOption.BidPrice,
                    askPrice: longOption.AskPrice,
                    impliedVolatility: longOptionGreeks?.ImpliedVolatility ?? 0.0,
                    delta: longOptionGreeks?.Delta ?? 0.0,
                    gamma: longOptionGreeks?.Gamma ?? 0.0,
                    theta: longOptionGreeks?.Theta ?? 0.0));
        }
        finally
        {
            marketDataSnapshotApi.StreamIds.Remove(shortRequestId);
            marketDataSnapshotApi.StreamIds.Remove(longRequestId);
            marketDataSnapshotApi.Stop();
        }
        await context.ReplyAsync(q.Subject.ThreadId, GetFuturesOptionSpreadDataQuery.Verb, new ServiceResult<FuturesOptionSpreadDataReadModel>(spreadData));
    }
}
