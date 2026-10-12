using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct HistoricalObservationParameter(FuturesTradeSessionBarReadModel Value) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(Value.MarketSeriesIdentity.Format()),
        Text(Value.TimeFrame.ToString()),
        Integer(checked(Value.ValueDate.Year * 100 + Value.ValueDate.Month)),
        TimestampTz(Value.IntervalEndUtc.UtcDateTime),
        Uuid(Value.ObservationId.Value),
        Text(Value.ContractId),
        Date(Value.ValueDate),
        TimestampTz(Value.IntervalStartUtc.UtcDateTime),
        TimestampTz(Value.IntervalEndUtc.UtcDateTime),
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = Value.Open },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = Value.High },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = Value.Low },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = Value.Close },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = Value.Volume },
        Bigint(Value.TradeCount),
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = Value.PriceVolumeSum },
        Bigint(Value.FirstSourceSequence),
        Bigint(Value.LastSourceSequence),
        TimestampTz(Value.FirstMarketEventUtc.UtcDateTime),
        TimestampTz(Value.LastMarketEventUtc.UtcDateTime),
        TimestampTz(Value.CalculatedAtUtc.UtcDateTime),
        Integer((int)Value.SchemaVersion),
        Text(Value.CalculationVersion),
        Boolean(Value.IsComplete),
        Boolean(Value.IsValid)
    } : new object?[]
    {
        Value.MarketSeriesIdentity.Format(), Value.TimeFrame.ToString(), checked(Value.ValueDate.Year * 100 + Value.ValueDate.Month),
        Value.IntervalEndUtc.UtcDateTime, Value.ObservationId.Value, Value.ContractId, Value.ValueDate,
        Value.IntervalStartUtc.UtcDateTime, Value.IntervalEndUtc.UtcDateTime, Value.Open, Value.High,
        Value.Low, Value.Close, Value.Volume, Value.TradeCount, Value.PriceVolumeSum,
        Value.FirstSourceSequence, Value.LastSourceSequence, Value.FirstMarketEventUtc.UtcDateTime,
        Value.LastMarketEventUtc.UtcDateTime, Value.CalculatedAtUtc.UtcDateTime,
        (int)Value.SchemaVersion, Value.CalculationVersion, Value.IsComplete, Value.IsValid
    };
}
