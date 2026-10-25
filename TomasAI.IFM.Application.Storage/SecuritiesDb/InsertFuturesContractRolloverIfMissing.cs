using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesContractRolloverIfMissing(
    string symbol,
    DateTime createdOn,
    string createdBy) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new Npgsql.NpgsqlParameter[] { Text(symbol), TimestampTz(createdOn), Text(createdBy) } : new object?[] { symbol, createdOn, createdBy };
}
