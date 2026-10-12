using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesAtrSignal(string contractId, DateOnly valueDate, string timePeriod, int periodLength, TimeOnly timestamp, decimal futuresPrice, double atrValue, double trueRange, string atr, string atrStrength, string? configurationId, Guid? observationId, DateTime? marketDataAsOf, long? sourceSequence, string? calculationVersion, string? calculationMethod, int? schemaVersion, bool? isValid, double? previousAtrValue, double? atrBaseline, double? atrRatio, bool isWarm) : IBindValue
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
        Double(atrValue),
        Double(trueRange),
        Text(atr),
        Text(atrStrength),
        Text(configurationId),
        Uuid(observationId),
        TimestampTz(marketDataAsOf),
        Bigint(sourceSequence),
        Text(calculationVersion),
        Text(calculationMethod),
        Integer(schemaVersion),
        Boolean(isValid),
        Double(previousAtrValue),
        Double(atrBaseline),
        Double(atrRatio),
        Boolean(isWarm)
    } : new object?[] { contractId, valueDate, timePeriod, periodLength, timestamp, futuresPrice, atrValue, trueRange, atr, atrStrength, configurationId, observationId, marketDataAsOf, sourceSequence, calculationVersion, calculationMethod, schemaVersion, isValid, previousAtrValue, atrBaseline, atrRatio, isWarm };
}
