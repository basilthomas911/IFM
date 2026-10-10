using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Query.Actor;
using TomasAI.IFM.Application.MarketData.Contracts;

using System.Runtime.CompilerServices;
using TomasAI.IFM.Shared.Util;

namespace TomasAI.IFM.Domain.MarketData.Query;

internal static class OptionChainWindowInputs
{
    sealed record StoredInputs(decimal? Price, decimal? Deviation, DateOnly? ObservationDate);
    static readonly ConditionalWeakTable<object, AsyncReadCache<(string Contract, string Symbol, DateOnly ValueDate), StoredInputs>> Stored = new();

    internal static async Task<(decimal? Price, decimal? Deviation, DateOnly? ObservationDate)> GetAsync(
        IMarketDataQueryContext context, string symbol, string underlyingContractId,
        CancellationToken cancellationToken)
    {
        var valueDate = context.MarketSessionAuthority.Current.OperationalValueDate;
        decimal? price = null;
        if (context.MarketDataApi is { } marketDataApi && marketDataApi.GetRuntimeStatus().IsRunning)
        {
            try
            {
                price = await marketDataApi.GetFuturesPriceAsync(underlyingContractId).ConfigureAwait(false);
            }
            catch (MarketDataApiNotRunningException)
            {
                // The feed may stop between the status check and price read.
                // Stored EOD and Bollinger values still support chain discovery.
            }
        }
        var cache = Stored.GetValue(context.DbFactory, _ => new(128, TimeSpan.FromSeconds(30)));
        var stored = await cache.GetAsync((underlyingContractId, symbol, valueDate),
            async token =>
            {
                var result = await ReadStoredAsync(context, symbol, underlyingContractId, valueDate, token).ConfigureAwait(false);
                return new StoredInputs(result.Price, result.Deviation, result.ObservationDate);
            }, cancellationToken).ConfigureAwait(false);
        return (price ?? stored?.Price, stored?.Deviation, stored?.ObservationDate);
    }

    static async Task<(decimal? Price, decimal? Deviation, DateOnly? ObservationDate)> ReadStoredAsync(
        IMarketDataQueryContext context, string symbol, string underlyingContractId, DateOnly valueDate, CancellationToken cancellationToken)
    {
        decimal? price = null;
        var currentEod = await context.DbFactory.MarketDataDb.GetFuturesEodDataAsync(
            underlyingContractId, valueDate).ConfigureAwait(false);
        var deviationEod = currentEod;
        if (deviationEod is not { DailyStdDevAmount: > 0 } || !double.IsFinite(deviationEod.DailyStdDevAmount))
            deviationEod = await context.DbFactory.MarketDataDb.GetLastFuturesEodDataAsync(
                underlyingContractId, valueDate).ConfigureAwait(false);
        var series = MarketSeriesIdentity.ForFuturesSeries(
            new FuturesSeriesId(symbol, "calendar-front", "unadjusted", 1));
        var bollinger = await context.DbFactory.MarketDataDb.GetLatestFuturesBollingerBandSignalAsync(
            series, valueDate, cancellationToken).ConfigureAwait(false);
        var fallbackPrice = price ?? bollinger?.Price
            ?? (currentEod is { ClosePrice: > 0 } ? (decimal?)currentEod.ClosePrice : null)
            ?? (deviationEod is { ClosePrice: > 0 } ? (decimal?)deviationEod.ClosePrice : null);
        if (deviationEod is { DailyStdDevAmount: > 0 } && double.IsFinite(deviationEod.DailyStdDevAmount)
            && fallbackPrice is > 0)
            return (fallbackPrice, (decimal)deviationEod.DailyStdDevAmount, deviationEod.ValueDate);
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
            : (fallbackPrice, null, currentEod?.ValueDate ?? deviationEod?.ValueDate);
    }
}
