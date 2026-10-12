using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesBollingerBandSignal(
    string seriesKey, string timePeriod, string configurationId, int yearMonth,
    DateTime marketDataAsOf, Guid observationId, string contractId, DateOnly valueDate,
    decimal price, decimal? ema10Center, decimal? standardDeviation10, decimal? upper10,
    decimal? lower10, decimal? width10, decimal? position10, decimal? ema20Center,
    decimal? standardDeviation20, decimal? upper20, decimal? lower20, decimal? width20,
    decimal? position20, decimal? width20Baseline, decimal? width20Ratio, bool isWarm,
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
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = ema10Center },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = standardDeviation10 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = upper10 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = lower10 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = width10 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = position10 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = ema20Center },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = standardDeviation20 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = upper20 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = lower20 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = width20 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = position20 },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = width20Baseline },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = width20Ratio },
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
        contractId, valueDate, price, ema10Center, standardDeviation10, upper10, lower10,
        width10, position10, ema20Center, standardDeviation20, upper20, lower20, width20,
        position20, width20Baseline, width20Ratio, isWarm, sourceSequence, calculatedAt,
        schemaVersion, calculationVersion, calculationMethod, isValid
    };
}
