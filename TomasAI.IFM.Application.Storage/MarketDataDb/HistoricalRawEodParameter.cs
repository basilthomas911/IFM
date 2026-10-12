using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct HistoricalRawEodParameter(FuturesEodObservationReadModel Value) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(Value.MarketSeriesIdentity.Format()),
        Integer(checked(Value.ValueDate.Year * 100 + Value.ValueDate.Month)),
        Date(Value.ValueDate),
        Text(Value.ContractId),
        TimestampTz(Value.SessionStartUtc.UtcDateTime),
        TimestampTz(Value.SessionEndUtc.UtcDateTime),
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = Value.Open },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = Value.High },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = Value.Low },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = Value.Close },
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = Value.Volume },
        Bigint(Value.TradeCount),
        new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = Value.PriceVolumeSum },
        Uuid(Value.ObservationId.Value),
        Bigint(Value.FirstSourceSequence),
        Bigint(Value.LastSourceSequence),
        TimestampTz(Value.FirstMarketEventUtc.UtcDateTime),
        TimestampTz(Value.LastMarketEventUtc.UtcDateTime),
        Integer((int)Value.SchemaVersion),
        Boolean(Value.IsComplete),
        Boolean(Value.IsValid)
    } : new object?[]
    {
        Value.MarketSeriesIdentity.Format(), checked(Value.ValueDate.Year * 100 + Value.ValueDate.Month), Value.ValueDate, Value.ContractId,
        Value.SessionStartUtc.UtcDateTime, Value.SessionEndUtc.UtcDateTime, Value.Open, Value.High,
        Value.Low, Value.Close, Value.Volume, Value.TradeCount, Value.PriceVolumeSum,
        Value.ObservationId.Value, Value.FirstSourceSequence, Value.LastSourceSequence,
        Value.FirstMarketEventUtc.UtcDateTime, Value.LastMarketEventUtc.UtcDateTime,
        (int)Value.SchemaVersion, Value.IsComplete, Value.IsValid
    };
}
