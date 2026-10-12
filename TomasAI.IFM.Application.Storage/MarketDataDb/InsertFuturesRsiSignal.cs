using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesRsiSignal(string contractId, DateOnly valueDate, string timePeriod, int periodLength, TimeOnly timestamp, decimal price, decimal priceChange, decimal priceGain, decimal priceLoss, decimal averagePriceGain, decimal averagePriceLoss, double rs, double rsi, double rsiAverage, double rsiSlope, long sourceSequence, DateTime sourceEventTimestamp, string? configurationId, Guid? observationId, DateTime? marketDataAsOf, string? calculationVersion, string? calculationMethod, int? schemaVersion, bool? isValid, double? previousRsi, double? regimeSlope, bool isWarm) : IBindValue
{
    public bool PostgreSql { get; init; }

    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(contractId),
        Date(valueDate),
        Text(timePeriod),
        Integer(periodLength),
        new NpgsqlParameter<TimeOnly> { NpgsqlDbType = NpgsqlDbType.Time, TypedValue = timestamp },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = price },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = priceChange },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = priceGain },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = priceLoss },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = averagePriceGain },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = averagePriceLoss },
        Double(rs),
        Double(rsi),
        Double(rsiAverage),
        Double(rsiSlope),
        Bigint(sourceSequence),
        TimestampTz(sourceEventTimestamp),
        Text(configurationId),
        Uuid(observationId),
        TimestampTz(marketDataAsOf),
        Text(calculationVersion),
        Text(calculationMethod),
        Integer(schemaVersion),
        Boolean(isValid),
        Double(previousRsi),
        Double(regimeSlope),
        Boolean(isWarm)
    } : new object?[] { contractId, valueDate, timePeriod, periodLength, timestamp, price, priceChange, priceGain, priceLoss, averagePriceGain, averagePriceLoss, rs, rsi, rsiAverage, rsiSlope, sourceSequence, sourceEventTimestamp, configurationId, observationId, marketDataAsOf, calculationVersion, calculationMethod, schemaVersion, isValid, previousRsi, regimeSlope, isWarm };
}
