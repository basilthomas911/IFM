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
        var db = _dbFactory.MarketDataDb;
        var maxValueDate = await db
            .Use(MarketDataDbCql.GetFuturesItiSignalMaxTrendValueDate)
            .SetParameters(new GetFuturesItiSignalMaxTrendValueDate(
                contractId,
                valueDate,
                IntrinsicTimeTrendType.UpTrend.ToStringFast()))
            .ExecuteScalarAsync<DateOnly>(MapToMaxValueDate!, cancellationToken)
            .ConfigureAwait(false);

        return await db
            .Use(MarketDataDbCql.GetFuturesItiSignalMDI)
            .SetParameters(new GetFuturesItiSignalMDI(
                contractId,
                maxValueDate,
                GetIntrinsicTimeModes(),
                IntrinsicTimeTrendType.UpTrend.ToStringFast()))
            .ExecuteQueryAsync(MapToFuturesItiSignalMDI!, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ICollection<FuturesItiSignalMDIV2ReadModel>> GetFuturesItiSignalMDIByTrendAsync(
        string contractId,
        DateOnly valueDate,
        IntrinsicTimeTrendType intrinsicTimeTrend,
        int intrinsicTimeGroupId,
        CancellationToken cancellationToken)
    {
        var db = _dbFactory.MarketDataDb;
        var trend = intrinsicTimeTrend.ToStringFast();
        var intrinsicTimeModes = GetIntrinsicTimeModes();
        var maxValueDate = await db
            .Use(MarketDataDbCql.GetFuturesItiSignalMaxValueDateByTrend)
            .SetParameters(new GetFuturesItiSignalMaxValueDateByTrend(
                contractId,
                valueDate,
                intrinsicTimeModes,
                trend))
            .ExecuteScalarAsync(MapToMaxValueDate, cancellationToken)
            .ConfigureAwait(false);

        if (intrinsicTimeGroupId == -1)
        {
            intrinsicTimeGroupId = await db
                .Use(MarketDataDbCql.GetFuturesItiSignalMaxTimeGroupId)
                .SetParameters(new GetFuturesItiSignalMaxTimeGroupId(
                    contractId,
                    maxValueDate,
                    intrinsicTimeModes,
                    trend))
                .ExecuteScalarAsync(MapToMaxIntrinsicTimeGroupId!, cancellationToken)
                .ConfigureAwait(false);
        }

        return await db
            .Use(MarketDataDbCql.GetFuturesItiSignalMDIByTrend)
            .SetParameters(new GetFuturesItiSignalMDIByTrend(
                contractId,
                maxValueDate,
                intrinsicTimeModes,
                trend,
                intrinsicTimeGroupId))
            .ExecuteQueryAsync(MapToFuturesItiSignalMDI, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<FuturesTrendDirectionReadModel> GetFuturesTrendDirectionFromRSISignalAsync(
        string contractId,
        DateOnly valueDate,
        DateTime timestamp,
        int lookbackInterval,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken)
    {
        var db = _dbFactory.MarketDataDb;
        var upTrendCountTask = db
            .Use(MarketDataDbCql.GetFuturesRsiSignalUpTrendCount)
            .SetParameters(new GetFuturesRsiSignalUpTrendCount(
                contractId,
                valueDate,
                startTime,
                endTime))
            .ExecuteScalarAsync(MapToRsiTrendCount!, cancellationToken);
        var downTrendCountTask = db
            .Use(MarketDataDbCql.GetFuturesRsiSignalDownTrendCount)
            .SetParameters(new GetFuturesRsiSignalDownTrendCount(
                contractId,
                valueDate,
                startTime,
                endTime))
            .ExecuteScalarAsync(MapToRsiTrendCount!, cancellationToken);

        await Task.WhenAll(upTrendCountTask, downTrendCountTask).ConfigureAwait(false);
        var upTrendCount = await upTrendCountTask.ConfigureAwait(false);
        var downTrendCount = await downTrendCountTask.ConfigureAwait(false);
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
            .Use(MarketDataDbCql.GetLastFuturesTradeSignalBySymbol)
            .SetParameters(new GetLastFuturesTradeSignalBySymbol(contractIds, valueDate))
            .ExecuteSingleAsync(MapToFuturesTradeSignal, cancellationToken)
            .ConfigureAwait(false);
    }
}
