using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesTradeSignal(string contractId, DateOnly valueDate, string timePeriod, long sequenceId, TimeOnly timestamp, double mean, double stdDev, double futuresPrice, double priceChangePercent, double fundRiskPercent, double rsi, double rsiSlope, string trendType, string trendStrength, string tradeSignal, string tdi, string tdiStrength, double mdi, string mdiTrend, double mdiUpTrendLimit, double mdiDownTrendLimit, double upTrendingTrigger, double downTrendingTrigger, double entryTrigger, double exitTrigger, double trendDelta, double trendExtreme, double trendReversal, decimal fiftyDma, decimal twoHundredDma, string tradeExecuteState) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate),
        Text(timePeriod),
        Bigint(sequenceId),
        new NpgsqlParameter<TimeOnly> { NpgsqlDbType = NpgsqlDbType.Time, TypedValue = timestamp },
        Double(mean),
        Double(stdDev),
        Double(futuresPrice),
        Double(priceChangePercent),
        Double(fundRiskPercent),
        Double(rsi),
        Double(rsiSlope),
        Text(trendType),
        Text(trendStrength),
        Text(tradeSignal),
        Text(tdi),
        Text(tdiStrength),
        Double(mdi),
        Text(mdiTrend),
        Double(mdiUpTrendLimit),
        Double(mdiDownTrendLimit),
        Double(upTrendingTrigger),
        Double(downTrendingTrigger),
        Double(entryTrigger),
        Double(exitTrigger),
        Double(trendDelta),
        Double(trendExtreme),
        Double(trendReversal),
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = fiftyDma },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = twoHundredDma },
        Text(tradeExecuteState)
    } : new object?[] { contractId, valueDate, timePeriod, sequenceId, timestamp, mean, stdDev, futuresPrice, priceChangePercent, fundRiskPercent, rsi, rsiSlope, trendType, trendStrength, tradeSignal, tdi, tdiStrength, mdi, mdiTrend, mdiUpTrendLimit, mdiDownTrendLimit, upTrendingTrigger, downTrendingTrigger, entryTrigger, exitTrigger, trendDelta, trendExtreme, trendReversal, fiftyDma, twoHundredDma, tradeExecuteState };
}
