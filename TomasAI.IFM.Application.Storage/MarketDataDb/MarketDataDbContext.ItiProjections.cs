using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.PredictiveModel.Shared.FuturesItiTrend;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Extensions;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

public partial class MarketDataDbContext
{
    const string FuturesItiSignalQueryProjection = "futures_iti_signal_queries";

    static FuturesItiProjectionScopeData MapToFuturesItiProjectionScope<TDataRecord>(TDataRecord row)
        where TDataRecord : IObjectDataRecord
        => new(row.GetString(0), row.GetDateOnly(1), row.GetString(2), row.GetString(3));

    static ulong GetFuturesItiSignalIdentity(FuturesItiSignalV2ReadModel row)
    {
        var hash = MarketDataProjectionHash.Start();
        hash = MarketDataProjectionHash.Add(hash, row.ContractId);
        hash = MarketDataProjectionHash.Add(hash, row.ValueDate);
        hash = MarketDataProjectionHash.Add(hash, row.TimePeriod.ToStringFast());
        hash = MarketDataProjectionHash.Add(hash, row.SequenceId);
        hash = MarketDataProjectionHash.Add(hash, row.IntrinsicTime.Ticks);
        hash = MarketDataProjectionHash.Add(hash, row.IntrinsicTimeGroupId);
        hash = MarketDataProjectionHash.Add(hash, row.IntrinsicTimeLength);
        hash = MarketDataProjectionHash.Add(hash, row.IntrinsicPrice);
        hash = MarketDataProjectionHash.Add(hash, row.IntrinsicTimeTrend.ToStringFast());
        hash = MarketDataProjectionHash.Add(hash, row.IntrinsicTimeMode.ToStringFast());
        hash = MarketDataProjectionHash.Add(hash, row.TrendPrice);
        hash = MarketDataProjectionHash.Add(hash, row.TrendExtreme);
        hash = MarketDataProjectionHash.Add(hash, row.TrendReversal);
        hash = MarketDataProjectionHash.Add(hash, row.TrendDelta);
        hash = MarketDataProjectionHash.Add(hash, row.TargetDelta);
        hash = MarketDataProjectionHash.Add(hash, row.Lambda);
        hash = MarketDataProjectionHash.Add(hash, row.TradingDays);
        hash = MarketDataProjectionHash.Add(hash, row.Threshold);
        hash = MarketDataProjectionHash.Add(hash, row.UpTrendTrigger);
        hash = MarketDataProjectionHash.Add(hash, row.DownTrendTrigger);
        hash = MarketDataProjectionHash.Add(hash, row.TradeState.ToStringFast());
        hash = MarketDataProjectionHash.Add(hash, row.BandLevel);
        return MarketDataProjectionHash.Add(hash, row.ReversalLevel);
    }

    static string GetFuturesItiDayScopeKey(string contractId, DateOnly valueDate)
        => $"day:{contractId.Length}:{contractId}:{valueDate:yyyyMMdd}";

    static string GetFuturesItiMonthScopeKey(string contractId, int yearMonth)
        => $"month:{contractId.Length}:{contractId}:{yearMonth}";

    static string GetFuturesItiTimelineScopeKey(
        string contractId,
        string intrinsicTimeTrend,
        string intrinsicTimeMode,
        int yearMonth)
        => $"timeline:{contractId.Length}:{contractId}:{intrinsicTimeTrend.Length}:{intrinsicTimeTrend}:{intrinsicTimeMode.Length}:{intrinsicTimeMode}:{yearMonth}";

    static string[] GetFuturesItiProjectionScopeKeys(
        string contractId,
        DateOnly valueDate,
        string intrinsicTimeTrend,
        string intrinsicTimeMode)
    {
        var yearMonth = ToYearMonth(valueDate);
        return
        [
            GetFuturesItiDayScopeKey(contractId, valueDate),
            GetFuturesItiMonthScopeKey(contractId, yearMonth),
            GetFuturesItiTimelineScopeKey(
                contractId,
                intrinsicTimeTrend,
                intrinsicTimeMode,
                yearMonth)
        ];
    }

    static InsertFuturesItiSignal CreateFuturesItiSignalParameters(
        FuturesItiSignalV2ReadModel e,
        long sequenceId)
        => new(
            e.ContractId,
            e.ValueDate,
            e.TimePeriod.ToStringFast(),
            sequenceId,
            e.IntrinsicTime,
            e.IntrinsicTimeGroupId,
            e.IntrinsicTimeLength,
            e.IntrinsicPrice,
            e.IntrinsicTimeTrend.ToStringFast(),
            e.IntrinsicTimeMode.ToStringFast(),
            e.TrendPrice,
            e.TrendExtreme,
            e.TrendReversal,
            e.TrendDelta,
            e.TargetDelta,
            e.Lambda,
            e.TradingDays,
            e.Threshold,
            e.UpTrendTrigger,
            e.DownTrendTrigger,
            e.TradeState.ToStringFast(),
            e.BandLevel,
            e.ReversalLevel);

    static InsertFuturesItiSignalByContractMonth CreateFuturesItiSignalMonthParameters(
        FuturesItiSignalV2ReadModel e,
        long sequenceId)
        => new(
            ToYearMonth(e.ValueDate),
            e.ContractId,
            e.ValueDate,
            e.TimePeriod.ToStringFast(),
            sequenceId,
            e.IntrinsicTime,
            e.IntrinsicTimeGroupId,
            e.IntrinsicTimeLength,
            e.IntrinsicPrice,
            e.IntrinsicTimeTrend.ToStringFast(),
            e.IntrinsicTimeMode.ToStringFast(),
            e.TrendPrice,
            e.TrendExtreme,
            e.TrendReversal,
            e.TrendDelta,
            e.TargetDelta,
            e.Lambda,
            e.TradingDays,
            e.Threshold,
            e.UpTrendTrigger,
            e.DownTrendTrigger,
            e.TradeState.ToStringFast(),
            e.BandLevel,
            e.ReversalLevel);

    static UpsertFuturesItiTimeFrameState CreateFuturesItiTimeFrameStateParameters(
        FuturesItiSignalV2ReadModel e,
        long sequenceId)
        => new(
            e.ContractId,
            e.TimePeriod.ToStringFast(),
            GetFuturesItiCalendarBucketStart(e.ValueDate, e.TimePeriod),
            e.TimeFrameStartValueDate == default ? e.ValueDate : e.TimeFrameStartValueDate,
            e.ValueDate,
            sequenceId,
            e.IntrinsicTime,
            e.IntrinsicTimeGroupId,
            e.IntrinsicTimeLength,
            e.IntrinsicPrice,
            e.IntrinsicTimeTrend.ToStringFast(),
            e.IntrinsicTimeMode.ToStringFast(),
            e.TrendPrice,
            e.TrendExtreme,
            e.TrendReversal,
            e.TrendDelta,
            e.TargetDelta,
            e.Lambda,
            e.TradingDays,
            e.Threshold,
            e.UpTrendTrigger,
            e.DownTrendTrigger,
            e.TradeState.ToStringFast(),
            e.BandAnchorPrice == 0 ? e.IntrinsicPrice : e.BandAnchorPrice,
            e.BandPercentage == 0 ? 0.10 : e.BandPercentage,
            e.BandSize == 0 ? e.Threshold * 0.10 : e.BandSize,
            e.BandLevel,
            e.ReversalLevel);

    static DateOnly GetFuturesItiCalendarBucketStart(
        DateOnly valueDate,
        TimeFrameType period)
        => period switch
        {
            TimeFrameType.Daily => valueDate,
            TimeFrameType.Weekly => valueDate.AddDays(
                -(((int)valueDate.DayOfWeek + 6) % 7)),
            TimeFrameType.Monthly => new DateOnly(valueDate.Year, valueDate.Month, 1),
            _ => valueDate
        };

    async Task<ICollection<FuturesItiSignalV2ReadModel>> ReadCanonicalFuturesItiSignalsAsync(
        IReadOnlyCollection<string> contractIds,
        DateOnly startDate,
        DateOnly endDate)
    {
        var db = _dbFactory.MarketDataDb;
        List<FuturesItiSignalV2ReadModel> rows = [];
        foreach (var batch in contractIds.Chunk(ProjectionReadConcurrency))
        {
            var reads = batch.Select(async contractId => await db
                .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignalsCanonicalByContract)}", MarketDataDbCql.GetFuturesItiSignalsCanonicalByContract)
                .SetParameters(new GetFuturesItiSignalsCanonicalByContract(contractId))
                .ExecuteQueryAsync(MapToFuturesItiSignal!));
            foreach (var values in await Task.WhenAll(reads))
                rows.AddRange(values);
        }
        return [.. rows
            .Where(row => row.ValueDate >= startDate && row.ValueDate <= endDate)
            .OrderBy(static row => row.ValueDate)
            .ThenBy(static row => row.SequenceId)];
    }

    async Task<ICollection<FuturesItiSignalV2ReadModel>> ReadFuturesItiSignalsByDateRangeAsync(
        IReadOnlyCollection<string> contractIds,
        DateOnly startDate,
        DateOnly endDate)
    {
        if (contractIds.Count == 0 || endDate < startDate)
            return [];

        var yearMonths = GetYearMonths(startDate, endDate).ToArray();
        var scopes = contractIds.SelectMany(contractId =>
            yearMonths.Select(yearMonth => GetFuturesItiMonthScopeKey(contractId, yearMonth))).ToArray();
        var stamp = await GetProjectionScopeReadStampAsync(FuturesItiSignalQueryProjection, scopes);
        if (stamp is null)
            return await ReadCanonicalFuturesItiSignalsAsync(contractIds, startDate, endDate);

        var db = _dbFactory.MarketDataDb;
        var partitions = contractIds.SelectMany(contractId =>
            yearMonths.Select(yearMonth => (contractId, yearMonth))).ToArray();
        List<FuturesItiSignalV2ReadModel> rows = [];
        foreach (var batch in partitions.Chunk(ProjectionReadConcurrency))
        {
            var requests = batch.Select(async partition =>
            {
                var monthStart = GetMonthStart(partition.yearMonth);
                var monthEnd = GetMonthEnd(partition.yearMonth);
                return await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignalsByContractMonth)}", MarketDataDbCql.GetFuturesItiSignalsByContractMonth)
                    .SetParameters(new GetFuturesItiSignalsByContractMonth(
                        partition.contractId,
                        partition.yearMonth,
                        startDate > monthStart ? startDate : monthStart,
                        endDate < monthEnd ? endDate : monthEnd))
                    .ExecuteQueryAsync(MapToFuturesItiSignal!);
            });
            foreach (var values in await Task.WhenAll(requests))
                rows.AddRange(values);
        }
        if (!await IsProjectionScopeReadStampValidAsync(stamp.Value))
            return await ReadCanonicalFuturesItiSignalsAsync(contractIds, startDate, endDate);

        return [.. rows
            .OrderBy(static row => row.ValueDate)
            .ThenBy(static row => row.SequenceId)];
    }

    async Task<ICollection<FuturesItiSignalV2ReadModel>> ReadFuturesItiDayModeAsync(
        string contractId,
        DateOnly valueDate,
        IntrinsicTimeModeType intrinsicTimeMode,
        long? afterSequenceId = null,
        CancellationToken cancellationToken = default)
    {
        var stamp = await GetProjectionScopeReadStampAsync(
            FuturesItiSignalQueryProjection,
            [GetFuturesItiDayScopeKey(contractId, valueDate)]);
        if (stamp is not null)
        {
            var mode = intrinsicTimeMode.ToStringFast();
            var query = afterSequenceId.HasValue
                ? _dbFactory.MarketDataDb
                    .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignalsByContractDayModeAfterSequence)}", MarketDataDbCql.GetFuturesItiSignalsByContractDayModeAfterSequence)
                    .SetParameters(new GetFuturesItiSignalsByContractDayModeAfterSequence(
                        contractId, valueDate, mode, afterSequenceId.Value))
                : _dbFactory.MarketDataDb
                    .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignalsByContractDayMode)}", MarketDataDbCql.GetFuturesItiSignalsByContractDayMode)
                    .SetParameters(new GetFuturesItiSignalsByContractDayMode(contractId, valueDate, mode));
            var projected = await query.ExecuteQueryAsync(MapToFuturesItiSignal!, cancellationToken)
                .ConfigureAwait(false);
            if (await IsProjectionScopeReadStampValidAsync(stamp.Value))
                return projected;
        }

        var canonical = await _dbFactory.MarketDataDb
            .Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignalsCanonicalByContractDay)}", MarketDataDbCql.GetFuturesItiSignalsCanonicalByContractDay)
            .SetParameters(new GetFuturesItiSignalsCanonicalByContractDay(contractId, valueDate))
            .ExecuteQueryAsync(MapToFuturesItiSignal!, cancellationToken)
            .ConfigureAwait(false);
        return [.. canonical
            .Where(row => row.IntrinsicTimeMode == intrinsicTimeMode &&
                (!afterSequenceId.HasValue || row.SequenceId > afterSequenceId.Value))
            .OrderByDescending(static row => row.SequenceId)];
    }

    async Task<FuturesItiSignalV2ReadModel?> ReadLastFuturesItiTrendModeAsync(
        string contractId,
        DateOnly valueDate,
        IntrinsicTimeTrendType intrinsicTimeTrend,
        IntrinsicTimeModeType intrinsicTimeMode,
        CancellationToken cancellationToken = default)
    {
        var db = _dbFactory.MarketDataDb;
        var targetMonth = ToYearMonth(valueDate);
        var months = (await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetMarketDataProjectionMonths)}", MarketDataDbCql.GetMarketDataProjectionMonths)
            .SetParameters(new GetMarketDataProjectionMonths(FuturesItiSignalQueryProjection, targetMonth))
            .ExecuteQueryAsync(MapToYearMonth, cancellationToken)
            .ConfigureAwait(false)).ToArray();
        var trend = intrinsicTimeTrend.ToStringFast();
        var mode = intrinsicTimeMode.ToStringFast();
        var scopes = months
            .Select(month => GetFuturesItiTimelineScopeKey(contractId, trend, mode, month))
            .Concat(GetProjectionGuardScopeKeys())
            .ToArray();
        var stamp = await GetProjectionScopeReadStampAsync(FuturesItiSignalQueryProjection, scopes);
        if (stamp is not null)
        {
            FuturesItiSignalV2ReadModel? projected = null;
            foreach (var yearMonth in months)
            {
                projected = await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetLastFuturesItiSignalByTrendModeMonth)}", MarketDataDbCql.GetLastFuturesItiSignalByTrendModeMonth)
                    .SetParameters(new GetLastFuturesItiSignalByTrendModeMonth(
                        contractId, trend, mode, yearMonth,
                        yearMonth == targetMonth ? valueDate : GetMonthEnd(yearMonth)))
                    .ExecuteSingleAsync(MapToFuturesItiSignal!, cancellationToken)
                    .ConfigureAwait(false);
                if (projected is not null)
                    break;
            }
            if (await IsProjectionScopeReadStampValidAsync(stamp.Value))
                return projected;
        }

        return (await db.Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.GetFuturesItiSignalsCanonicalByContract)}", MarketDataDbCql.GetFuturesItiSignalsCanonicalByContract)
                .SetParameters(new GetFuturesItiSignalsCanonicalByContract(contractId))
                .ExecuteQueryAsync(MapToFuturesItiSignal!, cancellationToken)
                .ConfigureAwait(false))
            .Where(row => row.ValueDate <= valueDate &&
                row.IntrinsicTimeTrend == intrinsicTimeTrend &&
                row.IntrinsicTimeMode == intrinsicTimeMode)
            .OrderByDescending(static row => row.ValueDate)
            .ThenByDescending(static row => row.SequenceId)
            .FirstOrDefault();
    }

    static FuturesItiSignalMDIV2ReadModel ToFuturesItiSignalMdi(FuturesItiSignalV2ReadModel row)
        => new(
            contractId: row.ContractId,
            valueDate: row.ValueDate,
            intrinsicTime: row.IntrinsicTime,
            trendType: row.IntrinsicTimeTrend,
            mdi: row.IntrinsicPrice);
}

readonly record struct FuturesItiProjectionScopeData(
    string ContractId,
    DateOnly ValueDate,
    string IntrinsicTimeTrend,
    string IntrinsicTimeMode);
