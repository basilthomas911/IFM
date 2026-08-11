using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

public static class GetFuturesOptionSpreadData
{
    internal static async ValueTask<FuturesOptionSpreadDataReadModel> GetFuturesOptionSpreadDataAsync(
        this GetFuturesOptionSpreadDataQuery q, ApplicationMarketDataApi marketDataApi)
        => await GetFuturesOptionSpreadDataAsync(
            marketDataApi,
            q.QueryForShortOptionContract.ContractId,
            q.QueryForLongOptionContract.ContractId);

    internal static async ValueTask<FuturesOptionSpreadDataReadModel> GetFuturesOptionSpreadDataAsync(
        ApplicationMarketDataApi marketDataApi,
        string shortFuturesOptionContractId,
        string longFuturesOptionContractId)
    {
        ArgumentNullException.ThrowIfNull(marketDataApi);
        ArgumentException.ThrowIfNullOrWhiteSpace(shortFuturesOptionContractId);
        ArgumentException.ThrowIfNullOrWhiteSpace(longFuturesOptionContractId);

        _ = await marketDataApi.GetFuturesOptionContractsAsync(
            [shortFuturesOptionContractId, longFuturesOptionContractId])
            .ConfigureAwait(false);

        return new FuturesOptionSpreadDataReadModel(
            CreateLeg(marketDataApi.GetFuturesOptionLastPriceReader(
                shortFuturesOptionContractId)),
            CreateLeg(marketDataApi.GetFuturesOptionLastPriceReader(
                longFuturesOptionContractId)));
    }

    private static FuturesOptionDataReadModel CreateLeg(
        IFuturesOptionLastPriceReader reader)
    {
        if (reader.TryGetLastQuoteWithGreeks(out var enriched))
            return CreateLeg(enriched.Tick, enriched.Greeks);
        if (reader.TryGetLastQuote(out var quote))
            return CreateLeg(quote, null);
        throw new InvalidOperationException(
            $"No live quote is available for futures option '{reader.FuturesOptionContractId}'.");
    }

    private static FuturesOptionDataReadModel CreateLeg(
        LastQuoteTickSnapshot quote,
        OptionGreeksSnapshot? greeks) =>
        new(
            bidPrice: Convert.ToDouble(quote.BidPrice ?? 0m),
            askPrice: Convert.ToDouble(quote.AskPrice ?? 0m),
            impliedVolatility: greeks is { IsValid: true, ImpliedVolatility: { } iv } ? iv : 0d,
            delta: greeks is { IsValid: true, Delta: { } delta } ? delta : 0d,
            gamma: greeks is { IsValid: true, Gamma: { } gamma } ? gamma : 0d,
            theta: greeks is { IsValid: true, Theta: { } theta } ? theta : 0d);
}
