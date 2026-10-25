using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesOptionContractBySymbolV2(string symbol, DateOnly contractMonth, string optionType, double strikePrice, string contractId) : IBindValue
{
    public bool PostgreSql { get; init; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3265", Justification = "Npgsql array parameters require the Array marker combined with their element type.")]
    public object Bind() => PostgreSql ? new NpgsqlParameter[] { Text(symbol), Date(contractMonth), Text(optionType), Double(strikePrice), Text(contractId) } : new object?[] { symbol, contractMonth, optionType, strikePrice, contractId };
}
