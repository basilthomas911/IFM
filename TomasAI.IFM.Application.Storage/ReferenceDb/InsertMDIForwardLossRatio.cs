using Npgsql;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S101", Justification = "Preserve the established public storage/serialized schema contract and identifiers consumed by callers.")]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertMDIForwardLossRatio(int mdi, string trendDirection, string tradeType, double forwardLossRatio, string createdBy, DateTime? createdOn, string updatedBy, DateTime? updatedOn) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Integer(mdi),
        Text(trendDirection),
        Text(tradeType),
        Double(forwardLossRatio),
        Text(createdBy),
        TimestampTz(createdOn),
        Text(updatedBy),
        TimestampTz(updatedOn)
    } : new object?[] { mdi, trendDirection, tradeType, forwardLossRatio, createdBy, createdOn, updatedBy, updatedOn };
}
