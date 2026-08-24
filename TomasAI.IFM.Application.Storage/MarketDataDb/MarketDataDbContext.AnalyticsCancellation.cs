using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage.Extensions;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

public partial class MarketDataDbContext
{
    public async Task<ICollection<FuturesItiSignalMDIV2ReadModel>> GetFuturesItiSignalMDIAsync(
        string contractId,
        DateOnly valueDate,
        CancellationToken cancellationToken)
    {
        var modes = new[]
        {
            IntrinsicTimeModeType.TrendExtremeChanged,
            IntrinsicTimeModeType.TrendReversalChanged,
            IntrinsicTimeModeType.TrendDirectionChanged
        };
        var latest = await Task.WhenAll(modes.Select(mode => ReadLastFuturesItiTrendModeAsync(
            contractId, valueDate, IntrinsicTimeTrendType.UpTrend, mode, cancellationToken)));
        var maxValueDate = latest.Where(static row => row is not null)
            .Select(static row => row!.ValueDate)
            .DefaultIfEmpty()
            .Max();
        if (maxValueDate == default)
            return [];
        var rows = await Task.WhenAll(modes.Select(mode => ReadFuturesItiDayModeAsync(
            contractId, maxValueDate, mode, cancellationToken: cancellationToken)));
        return [.. rows.SelectMany(static values => values).Select(ToFuturesItiSignalMdi)];
    }

    public async Task<ICollection<FuturesItiSignalMDIV2ReadModel>> GetFuturesItiSignalMDIByTrendAsync(
        string contractId,
        DateOnly valueDate,
        IntrinsicTimeTrendType intrinsicTimeTrend,
        int intrinsicTimeGroupId,
        CancellationToken cancellationToken)
    {
        _ = intrinsicTimeGroupId; // The legacy query never applied this argument.
        var modes = new[]
        {
            IntrinsicTimeModeType.TrendExtremeChanged,
            IntrinsicTimeModeType.TrendReversalChanged,
            IntrinsicTimeModeType.TrendDirectionChanged
        };
        var latest = await Task.WhenAll(modes.Select(mode => ReadLastFuturesItiTrendModeAsync(
            contractId, valueDate, intrinsicTimeTrend, mode, cancellationToken)));
        var maxValueDate = latest.Where(static row => row is not null)
            .Select(static row => row!.ValueDate)
            .DefaultIfEmpty()
            .Max();
        if (maxValueDate == default)
            return [];
        var rows = await Task.WhenAll(modes.Select(mode => ReadFuturesItiDayModeAsync(
            contractId, maxValueDate, mode, cancellationToken: cancellationToken)));
        return [.. rows.SelectMany(static values => values)
            .Where(row => row.IntrinsicTimeTrend == intrinsicTimeTrend)
            .Select(ToFuturesItiSignalMdi)];
    }

    public async Task<FuturesTrendDirectionReadModel> GetFuturesTrendDirectionFromRSISignalAsync(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        int periodLength,
        DateTime timestamp,
        int lookbackInterval,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken)
    {
        var db = _dbFactory.MarketDataDb;
        var rsiValues = await db
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesRsiSignalsForTrend)}", MarketDataDbCql.GetFuturesRsiSignalsForTrend)
            .SetParameters(new GetFuturesRsiSignalsForTrend(
                contractId,
                timePeriod.ToStringFast(),
                periodLength,
                valueDate,
                TimeOnly.FromDateTime(startTime),
                TimeOnly.FromDateTime(endTime)))
            .ExecuteQueryAsync(MapToRsi!, cancellationToken)
            .ConfigureAwait(false);
        var upTrendCount = rsiValues.Count(static rsi => rsi >= 50);
        var downTrendCount = rsiValues.Count(static rsi => rsi < 50);
        var trendDirection = upTrendCount.CompareTo(downTrendCount) switch
        {
            > 0 => FuturesTrendType.UpTrending,
            < 0 => FuturesTrendType.DownTrending,
            _ => FuturesTrendType.RangeBound
        };

        return new FuturesTrendDirectionReadModel(
            contractId,
            valueDate,
            TimeOnly.FromDateTime(DateTime.Now),
            lookbackInterval,
            upTrendCount,
            downTrendCount,
            trendDirection);
    }

    public async Task<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalBySymbolAsync(
        string symbol,
        DateOnly valueDate,
        CancellationToken cancellationToken)
    {
        var securitiesDb = (ISecuritiesDbReadContext)_dbFactory.SecuritiesDb;
        var contracts = await securitiesDb
            .GetFuturesContractsBySymbolAsync(symbol, cancellationToken)
            .ConfigureAwait(false);
        List<string> contractIds = [.. contracts.Select(static contract => contract.ContractId)];

        return await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesTradeSignalBySymbol)}", MarketDataDbCql.GetLastFuturesTradeSignalBySymbol)
            .SetParameters(new GetLastFuturesTradeSignalBySymbol(contractIds, valueDate))
            .ExecuteSingleAsync(MapToFuturesTradeSignal, cancellationToken)
            .ConfigureAwait(false);
    }
}
