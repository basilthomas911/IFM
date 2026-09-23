using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Query.Actor;

namespace TomasAI.IFM.Domain.MarketData.Query;

internal static class OptionChainWindowInputs
{
    internal static async Task<(decimal? Price, decimal? Deviation, DateOnly? ObservationDate)> GetAsync(
        IMarketDataQueryContext context, string symbol, string underlyingContractId,
        CancellationToken cancellationToken)
    {
        var valueDate = context.MarketSessionAuthority.Current.OperationalValueDate;
        var price = context.MarketDataApi is null ? null
            : await context.MarketDataApi.GetFuturesPriceAsync(underlyingContractId).ConfigureAwait(false);
        var eod = await context.DbFactory.MarketDataDb.GetFuturesEodDataAsync(
            underlyingContractId, valueDate).ConfigureAwait(false);
        if (eod is not { DailyStdDevAmount: > 0 } || !double.IsFinite(eod.DailyStdDevAmount))
            eod = await context.DbFactory.MarketDataDb.GetLastFuturesEodDataAsync(
                underlyingContractId, valueDate).ConfigureAwait(false);
        if (eod is { DailyStdDevAmount: > 0 } && double.IsFinite(eod.DailyStdDevAmount))
            return (price, (decimal)eod.DailyStdDevAmount, eod.ValueDate);

        var series = MarketSeriesIdentity.ForFuturesSeries(
            new FuturesSeriesId(symbol, "calendar-front", "unadjusted", 1));
        var bollinger = await context.DbFactory.MarketDataDb.GetLatestFuturesBollingerBandSignalAsync(
            series, valueDate, cancellationToken).ConfigureAwait(false);
        return bollinger is { IsProvisional: false, StandardDeviation20: > 0 }
            && bollinger.Metadata.IsValid
            ? (price, bollinger.StandardDeviation20, bollinger.Metadata.ValueDate)
            : (price, null, null);
    }
}
