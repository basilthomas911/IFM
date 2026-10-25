using Npgsql;
using NpgsqlTypes;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesOptionContract(string contractId, string description, string symbol, string localSymbol, string securityType, string currency, string exchange, string multiplier, DateOnly contractMonth, double strikePrice, string optionType, byte[]? referencePayload = null) : IBindValue
{
    public bool PostgreSql { get; init; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3265", Justification = "Npgsql array parameters require the Array marker combined with their element type.")]
    public object Bind() => PostgreSql ? new NpgsqlParameter[] { Text(contractId), Text(description), Text(symbol), Text(localSymbol), Text(securityType), Text(currency), Text(exchange), Text(multiplier), Date(contractMonth), Double(strikePrice), Text(optionType), Bytea(referencePayload) } : new object?[] { contractId, description, symbol, localSymbol, securityType, currency, exchange, multiplier, contractMonth, strikePrice, optionType, referencePayload };
}
