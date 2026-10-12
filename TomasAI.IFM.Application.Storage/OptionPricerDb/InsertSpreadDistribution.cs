using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.OptionPricerDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertSpreadDistribution(
    long Id,
    int TradeId,
    string TradeType,
    string TradeStatus,
    DateOnly ValueDate,
    int DaysToExpiry,
    double ForwardPrice,
    double LossProbability,
    double ShortVolatility,
    double LongVolatility,
    decimal LossThreshold,
    int LossThresholdCount,
    double ForwardLossRatio,
    DateTime CreatedOn) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql
        ? Values(Bigint(Id), Integer(TradeId), Date(ValueDate), Text(TradeType), Text(TradeStatus), Integer(DaysToExpiry), Double(ForwardPrice), Double(LossProbability), new NpgsqlParameter<decimal> { NpgsqlDbType = NpgsqlDbType.Numeric, TypedValue = LossThreshold }, Integer(LossThresholdCount), Double(ShortVolatility), Double(LongVolatility), Double(ForwardLossRatio), TimestampTz(OptionPricerPostgresTime.AsUtc(CreatedOn)))
        : new object?[]
    {
        Id,
        TradeId,
        ValueDate,
        TradeType,
        TradeStatus,
        DaysToExpiry,
        ForwardPrice,
        LossProbability,
        LossThreshold,
        LossThresholdCount,
        ShortVolatility,
        LongVolatility,
        ForwardLossRatio,
        CreatedOn
    };
}
