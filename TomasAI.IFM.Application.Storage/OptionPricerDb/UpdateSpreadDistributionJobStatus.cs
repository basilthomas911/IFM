using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.OptionPricerDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpdateSpreadDistributionJobStatus(
    int OrderId,
    int TradeId,
    DateOnly ValueDate,
    string JobStatus,
    DateTime JobCompleted,
    DateTime? JobFailed,
    bool InProgress) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql
        ? Values(TimestampTz(OptionPricerPostgresTime.AsUtc(JobCompleted)), TimestampTz(OptionPricerPostgresTime.AsUtc(JobFailed)), Text(JobStatus), Boolean(InProgress), Integer(OrderId), Integer(TradeId), Date(ValueDate))
        : new object?[]
    {
        JobCompleted,
        JobFailed,
        JobStatus,
        InProgress,
        OrderId,
        TradeId,
        ValueDate
    };
}
