using static TomasAI.IFM.Shared.Storage.BindValues;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesContract(string contractId, string description, string symbol, string localSymbol, string securityType, string currency, string exchange, string multiplier, DateOnly lastTradeDate, bool onTheRun, bool rollover, byte[]? referencePayload = null) : IBindValue
{
    public bool PostgreSql { get; init; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3265", Justification = "Npgsql array parameters require the Array marker combined with their element type.")]
    public object Bind() => PostgreSql ? new object?[] { Text(contractId), Text(description), Text(symbol), Text(localSymbol), Text(securityType), Text(currency), Text(exchange), Text(multiplier), Date(lastTradeDate), Boolean(onTheRun), Boolean(rollover), Bytea(referencePayload) } : new object?[] { contractId, description, symbol, localSymbol, securityType, currency, exchange, multiplier, lastTradeDate, onTheRun, rollover, referencePayload };
}
