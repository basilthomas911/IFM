using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertFuturesItiTimeFrameState(string contractId, string timePeriod, DateOnly calendarBucketStart, DateOnly timeFrameStartValueDate, DateOnly valueDate, long sequenceId, DateTime intrinsicTime, int intrinsicTimeGroupId, double intrinsicTimeLength, double intrinsicPrice, string intrinsicTimeTrend, string intrinsicTimeMode, double trendPrice, double trendExtreme, double trendReversal, double trendDelta, double targetDelta, double lambda, int tradingDays, double threshold, double upTrendTrigger, double downTrendTrigger, string tradeState, double bandAnchorPrice, double bandPercentage, double bandSize, double bandLevel, double reversalLevel) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Text(timePeriod),
        Date(calendarBucketStart),
        Date(timeFrameStartValueDate),
        Date(valueDate),
        Bigint(sequenceId),
        TimestampTz(intrinsicTime),
        Integer(intrinsicTimeGroupId),
        Double(intrinsicTimeLength),
        Double(intrinsicPrice),
        Text(intrinsicTimeTrend),
        Text(intrinsicTimeMode),
        Double(trendPrice),
        Double(trendExtreme),
        Double(trendReversal),
        Double(trendDelta),
        Double(targetDelta),
        Double(lambda),
        Integer(tradingDays),
        Double(threshold),
        Double(upTrendTrigger),
        Double(downTrendTrigger),
        Text(tradeState),
        Double(bandAnchorPrice),
        Double(bandPercentage),
        Double(bandSize),
        Double(bandLevel),
        Double(reversalLevel)
    } : new object?[] { contractId, timePeriod, calendarBucketStart, timeFrameStartValueDate, valueDate, sequenceId, intrinsicTime, intrinsicTimeGroupId, intrinsicTimeLength, intrinsicPrice, intrinsicTimeTrend, intrinsicTimeMode, trendPrice, trendExtreme, trendReversal, trendDelta, targetDelta, lambda, tradingDays, threshold, upTrendTrigger, downTrendTrigger, tradeState, bandAnchorPrice, bandPercentage, bandSize, bandLevel, reversalLevel };
}
