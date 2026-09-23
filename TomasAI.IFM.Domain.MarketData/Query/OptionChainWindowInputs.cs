using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
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
        var series = MarketSeriesIdentity.ForFuturesSeries(
            new FuturesSeriesId(symbol, "calendar-front", "unadjusted", 1));
        var bollinger = await context.DbFactory.MarketDataDb.GetLatestFuturesBollingerBandSignalAsync(
            series, valueDate, cancellationToken).ConfigureAwait(false);
        if (eod is { DailyStdDevAmount: > 0 } && double.IsFinite(eod.DailyStdDevAmount)
            && eod.ValueDate == valueDate && (price ?? bollinger?.Price) is > 0)
            return (price ?? bollinger!.Price, (decimal)eod.DailyStdDevAmount, valueDate);
        // Strike selection is approximate and value-date scoped; a provisional intraday
        // band is sufficient here and does not qualify option pricing inputs.
        if (bollinger is { StandardDeviation20: > 0 }
            && bollinger.Metadata.IsValid && bollinger.Metadata.ValueDate == valueDate)
            return (price ?? bollinger.Price, bollinger.StandardDeviation20, valueDate);
        var intraday = await context.DbFactory.MarketDataDb.GetLatestFuturesBollingerBandSignalForTimeFrameAsync(
            series, valueDate, TimeFrameType.FiveMinutes, cancellationToken).ConfigureAwait(false);
        return intraday is { StandardDeviation20: > 0, Price: > 0 }
            && intraday.Metadata.IsValid && intraday.Metadata.ValueDate == valueDate
            ? (price ?? intraday.Price, intraday.StandardDeviation20, valueDate)
            : (price, null, null);
    }
}
