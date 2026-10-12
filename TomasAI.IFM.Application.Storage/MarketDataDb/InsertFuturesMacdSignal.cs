using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;




[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesMacdSignal(
    string contractId,
    DateOnly valueDate,
    string timePeriod,
    int signalEmaPeriod,
    int fastEmaPeriod,
    int slowEmaPeriod,
    TimeOnly timestamp,
    decimal futuresPrice,
    double fastEma,
    double slowEma,
    double macdLine,
    double signalLine,
    double histogram,
    string macd,
    string macdStrength,
    string? configurationId,
    Guid? observationId,
    DateTime? marketDataAsOf,
    long? sourceSequence,
    string? calculationVersion,
    string? calculationMethod,
    int? schemaVersion,
    bool? isValid,
    bool isWarm,
    int observationCount) : IBindValue
{
    /// <summary>Selects typed native PostgreSQL parameters during provider migration.</summary>
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate),
        Text(timePeriod),
        Integer(signalEmaPeriod),
        Integer(fastEmaPeriod),
        Integer(slowEmaPeriod),
        new NpgsqlParameter<TimeOnly> { NpgsqlDbType = NpgsqlDbType.Time, TypedValue = timestamp },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = futuresPrice },
        Double(fastEma),
        Double(slowEma),
        Double(macdLine),
        Double(signalLine),
        Double(histogram),
        Text(macd),
        Text(macdStrength),
        Text(configurationId),
        Uuid(observationId),
        TimestampTz(marketDataAsOf),
        Bigint(sourceSequence),
        Text(calculationVersion),
        Text(calculationMethod),
        Integer(schemaVersion),
        Boolean(isValid),
        Boolean(isWarm),
        Integer(observationCount)
    } : new object?[] { contractId, valueDate, timePeriod, signalEmaPeriod, fastEmaPeriod, slowEmaPeriod, timestamp, futuresPrice, fastEma, slowEma, macdLine, signalLine, histogram, macd, macdStrength, configurationId, observationId, marketDataAsOf, sourceSequence, calculationVersion, calculationMethod, schemaVersion, isValid, isWarm, observationCount };
}
