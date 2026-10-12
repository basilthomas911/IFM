using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesVwapSignal(
    string contractId, DateOnly valueDate, string configurationId,
    DateTime asOfUtc, long lastTradeOrdinal, DateTime sessionStartUtc, DateTime sessionEndUtc,
    decimal cumulativePriceVolume, long cumulativeVolume, long eligibleTradeCount,
    long rejectedTradeCount, decimal lastPrice, decimal? vwap, decimal? priceMinusVwap,
    decimal? priceToVwapPercent, long lastTradeSourceSequence, Guid streamEpochId,
    bool isWarm, bool isValid, string invalidReason, bool isTickExact,
    string calculationMethod, int schemaVersion, string calculationVersion) : IBindValue
{
    /// <summary>Selects native typed parameters for the PostgreSQL provider.</summary>
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate),
        Text(configurationId),
        TimestampTz(asOfUtc),
        Bigint(lastTradeOrdinal),
        TimestampTz(sessionStartUtc),
        TimestampTz(sessionEndUtc),
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = cumulativePriceVolume },
        Bigint(cumulativeVolume),
        Bigint(eligibleTradeCount),
        Bigint(rejectedTradeCount),
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = lastPrice },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = vwap },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = priceMinusVwap },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = priceToVwapPercent },
        Bigint(lastTradeSourceSequence),
        Uuid(streamEpochId),
        Boolean(isWarm),
        Boolean(isValid),
        Text(invalidReason),
        Boolean(isTickExact),
        Text(calculationMethod),
        Integer(schemaVersion),
        Text(calculationVersion)
    } : new object?[]
    {
        contractId, valueDate, configurationId, asOfUtc, lastTradeOrdinal,
        sessionStartUtc, sessionEndUtc, cumulativePriceVolume, cumulativeVolume,
        eligibleTradeCount, rejectedTradeCount, lastPrice, vwap, priceMinusVwap,
        priceToVwapPercent, lastTradeSourceSequence, streamEpochId, isWarm, isValid,
        invalidReason, isTickExact, calculationMethod, schemaVersion, calculationVersion
    };
}
