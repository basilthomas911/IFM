using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesAdxSignal(string contractId, DateOnly valueDate, string timePeriod, int periodLength, TimeOnly timestamp, decimal futuresPrice, double plusDI, double minusDI, double adxValue, string adx, string adxStrength, string? configurationId, Guid? observationId, DateTime? marketDataAsOf, long? sourceSequence, string? calculationVersion, string? calculationMethod, int? schemaVersion, bool? isValid, bool isWarm = false) : IBindValue
{
    /// <summary>Selects typed native PostgreSQL parameters during provider migration.</summary>
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate),
        Text(timePeriod),
        Integer(periodLength),
        new NpgsqlParameter<TimeOnly> { NpgsqlDbType = NpgsqlDbType.Time, TypedValue = timestamp },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = futuresPrice },
        Double(plusDI),
        Double(minusDI),
        Double(adxValue),
        Text(adx),
        Text(adxStrength),
        Text(configurationId),
        Uuid(observationId),
        TimestampTz(marketDataAsOf),
        Bigint(sourceSequence),
        Text(calculationVersion),
        Text(calculationMethod),
        Integer(schemaVersion),
        Boolean(isValid),
        Boolean(isWarm)
    } : new object?[] { contractId, valueDate, timePeriod, periodLength, timestamp, futuresPrice, plusDI, minusDI, adxValue, adx, adxStrength, configurationId, observationId, marketDataAsOf, sourceSequence, calculationVersion, calculationMethod, schemaVersion, isValid };
}
