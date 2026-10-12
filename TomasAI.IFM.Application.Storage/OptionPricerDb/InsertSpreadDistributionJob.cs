using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.OptionPricerDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertSpreadDistributionJob(
    int OrderId,
    int TradeId,
    string TradeType,
    string TradeStatus,
    DateOnly ValueDate,
    int DaysToExpiry,
    DateTime JobSubmitted,
    string JobStatus,
    DateTime? JobCompleted,
    DateTime? JobFailed,
    bool InProgress,
    double LossProbabilityFactor) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql
        ? Values(Integer(OrderId), Integer(TradeId), Text(TradeType), Text(TradeStatus), Date(ValueDate), Integer(DaysToExpiry), TimestampTz(OptionPricerPostgresTime.AsUtc(JobSubmitted)), Text(JobStatus), TimestampTz(OptionPricerPostgresTime.AsUtc(JobCompleted)), TimestampTz(OptionPricerPostgresTime.AsUtc(JobFailed)), Boolean(InProgress), Double(LossProbabilityFactor))
        : new object?[]
    {
        OrderId,
        TradeId,
        TradeType,
        TradeStatus,
        ValueDate,
        DaysToExpiry,
        JobSubmitted,
        JobStatus,
        JobCompleted,
        JobFailed,
        InProgress,
        LossProbabilityFactor
    };
}
