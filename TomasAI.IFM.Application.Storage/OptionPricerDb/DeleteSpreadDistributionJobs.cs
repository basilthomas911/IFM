using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.OptionPricerDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteSpreadDistributionJobs(int OrderId, int TradeId) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql
        ? Values(Integer(OrderId), Integer(TradeId))
        : new object?[]
    { OrderId, TradeId };
}
