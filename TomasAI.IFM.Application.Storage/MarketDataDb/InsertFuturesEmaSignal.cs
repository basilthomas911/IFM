using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesEmaSignal(
    string seriesKey, string timePeriod, string configurationId, int yearMonth,
    DateTime marketDataAsOf, Guid observationId, string contractId, DateOnly valueDate,
    decimal price, decimal? ema10, decimal? previousEma10, decimal? ema10Slope,
    decimal? ema20, decimal? previousEma20, decimal? ema20Slope,
    decimal? ema50, decimal? previousEma50, decimal? ema50Slope,
    decimal? ema200, decimal? previousEma200, decimal? ema200Slope, bool isWarm,
    long sourceSequence, DateTime calculatedAt, int schemaVersion,
    string calculationVersion, string calculationMethod, bool isValid) : IBindValue
{
    /// <summary>Selects native typed parameters for the PostgreSQL provider.</summary>
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(seriesKey),
        Text(timePeriod),
        Text(configurationId),
        Integer(yearMonth),
        TimestampTz(marketDataAsOf),
        Uuid(observationId),
        Text(contractId),
        Date(valueDate),
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = price },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = ema10 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = previousEma10 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = ema10Slope },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = ema20 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = previousEma20 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = ema20Slope },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = ema50 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = previousEma50 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = ema50Slope },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = ema200 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = previousEma200 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = ema200Slope },
        Boolean(isWarm),
        Bigint(sourceSequence),
        TimestampTz(calculatedAt),
        Integer(schemaVersion),
        Text(calculationVersion),
        Text(calculationMethod),
        Boolean(isValid)
    } : new object?[]
    {
        seriesKey, timePeriod, configurationId, yearMonth, marketDataAsOf, observationId,
        contractId, valueDate, price, ema10, previousEma10, ema10Slope,
        ema20, previousEma20, ema20Slope, ema50, previousEma50, ema50Slope,
        ema200, previousEma200, ema200Slope, isWarm, sourceSequence, calculatedAt,
        schemaVersion, calculationVersion, calculationMethod, isValid
    };
}
