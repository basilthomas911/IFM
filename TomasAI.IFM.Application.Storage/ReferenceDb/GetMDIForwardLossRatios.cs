using Npgsql;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S101", Justification = "Preserve the established public storage/serialized schema contract and identifiers consumed by callers.")]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetMDIForwardLossRatios(string trendDirection, string tradeType) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Text(trendDirection),
        Text(tradeType)
    } : new object?[] { trendDirection, tradeType };
}
