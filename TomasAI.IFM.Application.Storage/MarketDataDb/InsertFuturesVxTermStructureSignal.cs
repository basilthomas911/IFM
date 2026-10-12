using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesVxTermStructureSignal(
    DateOnly valueDate, string configurationId, DateTime calculatedAt,
    long frontSourceSequence, long backSourceSequence,
    string frontContractId, DateOnly frontExpiry, decimal frontPrice,
    string backContractId, DateOnly backExpiry, decimal backPrice,
    decimal frontBackSpread, decimal frontBackRatio, decimal termStructurePercent,
    string termStructureState, decimal? priorFrontBackRatio,
    decimal? priorTermStructurePercent, DateTime frontSourceTimestamp,
    DateTime backSourceTimestamp, bool isWarm, bool isValid,
    int schemaVersion, string calculationVersion) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Date(valueDate),
        Text(configurationId),
        TimestampTz(calculatedAt),
        Bigint(frontSourceSequence),
        Bigint(backSourceSequence),
        Text(frontContractId),
        Date(frontExpiry),
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = frontPrice },
        Text(backContractId),
        Date(backExpiry),
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = backPrice },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = frontBackSpread },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = frontBackRatio },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = termStructurePercent },
        Text(termStructureState),
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = priorFrontBackRatio },
        new NpgsqlParameter<decimal?> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = priorTermStructurePercent },
        TimestampTz(frontSourceTimestamp),
        TimestampTz(backSourceTimestamp),
        Boolean(isWarm),
        Boolean(isValid),
        Integer(schemaVersion),
        Text(calculationVersion)
    } : new object?[]
    {
        valueDate, configurationId, calculatedAt, frontSourceSequence,
        backSourceSequence, frontContractId, frontExpiry, frontPrice,
        backContractId, backExpiry, backPrice, frontBackSpread, frontBackRatio,
        termStructurePercent, termStructureState, priorFrontBackRatio,
        priorTermStructurePercent, frontSourceTimestamp, backSourceTimestamp,
        isWarm, isValid, schemaVersion, calculationVersion
    };
}
