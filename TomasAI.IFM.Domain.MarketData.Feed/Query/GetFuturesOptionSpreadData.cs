using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

public static class GetFuturesOptionSpreadData
{
    internal static async ValueTask<FuturesOptionSpreadDataReadModel> GetFuturesOptionSpreadDataAsync(
        this GetFuturesOptionSpreadDataQuery q, IMarketDataSnapshotApi marketDataSnapshotApi)
        => await GetFuturesOptionSpreadDataAsync(
            marketDataSnapshotApi,
            q.ValueDate,
            q.MaturityDate,
            q.AssetPrice,
            q.RiskFreeRate,
            q.QueryForShortOptionContract,
            q.QueryForLongOptionContract);

    internal static async ValueTask<FuturesOptionSpreadDataReadModel> GetFuturesOptionSpreadDataAsync(
        IMarketDataSnapshotApi marketDataSnapshotApi,
        DateOnly valueDate,
        DateOnly maturityDate,
        double assetPrice,
        double riskFreeRate,
        FuturesOptionContractReadModel queryForShortOptionContract,
        FuturesOptionContractReadModel queryForLongOptionContract)
    {
        var shortRequestId = 0;
        var longRequestId = 0;
        FuturesOptionSpreadDataReadModel spreadData = default!;
        try
        {
            await marketDataSnapshotApi.StartAsync().ConfigureAwait(false);
            var (shortContract, longContract) = await marketDataSnapshotApi.GetFuturesOptionSpreadAsync(
                queryForShortOptionContract, queryForLongOptionContract);
            if (shortContract == null || longContract == null)
            {
                marketDataSnapshotApi.Stop();
                throw new InvalidOperationException("MarketDataFeedQueryState.GetFuturesOptionSpreadDataAsync: Unknown futures option contract definition(s)");
            }
            var shortOption = default(FuturesOptionTickDataV2ReadModel);
            shortRequestId = marketDataSnapshotApi.StreamIds.Add(shortContract.ContractId);
            await marketDataSnapshotApi.GetFuturesOptionPriceAsync(shortRequestId, valueDate, shortContract, e => shortOption = e);
            if (shortOption == null)
            {
                marketDataSnapshotApi.Stop();
                throw new InvalidOperationException($"MarketDataFeedQueryState.GetFuturesOptionSpreadDataAsync: Unknown short futures option contract definition '{shortContract.ContractId}'");
            }
            var shortOptionGreeks = marketDataSnapshotApi.GetFuturesOptionGreeks(
                valueDate, maturityDate, shortContract, shortOption.OptionPrice, assetPrice, riskFreeRate);

            var longOption = default(FuturesOptionTickDataV2ReadModel);
            longRequestId = marketDataSnapshotApi.StreamIds.Add(longContract.ContractId);
            await marketDataSnapshotApi.GetFuturesOptionPriceAsync(longRequestId, valueDate, longContract, e => longOption = e);
            if (longOption == null)
            {
                marketDataSnapshotApi.Stop();
                throw new InvalidOperationException($"MarketDataFeedQueryState.GetFuturesOptionSpreadDataAsync: Unknown long futures option contract definition '{longContract.ContractId}'");
            }
            var longOptionGreeks = marketDataSnapshotApi.GetFuturesOptionGreeks(
                valueDate, maturityDate, longContract, longOption.OptionPrice, assetPrice, riskFreeRate);
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
        return spreadData;
    }
}
