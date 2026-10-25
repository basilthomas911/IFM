using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesOptionContractById(string contractId, DateOnly contractMonth, string symbol, string optionType, double strikePrice) : IBindValue
{
    public bool PostgreSql { get; init; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3265", Justification = "Npgsql array parameters require the Array marker combined with their element type.")]
    public object Bind() => PostgreSql ? new NpgsqlParameter[] { Text(contractId), Date(contractMonth), Text(symbol), Text(optionType), Double(strikePrice) } : new object?[] { contractId, contractMonth, symbol, optionType, strikePrice };
}
