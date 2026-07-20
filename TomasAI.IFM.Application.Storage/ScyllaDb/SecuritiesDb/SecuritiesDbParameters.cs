using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.SecuritiesDb;

internal readonly record struct InsertFuturesContract(string contractId, string description, string symbol, string localSymbol, string securityType, string currency, string exchange, string multiplier, DateOnly lastTradeDate, bool currentlyTraded) : IBindValue
{
    public object Bind() => new { contractId, description, symbol, localSymbol, securityType, currency, exchange, multiplier, lastTradeDate, currentlyTraded };
}
internal readonly record struct DeleteFuturesContract(string contractId) : IBindValue
{
    public object Bind() => new { contractId };
}
internal readonly record struct DeleteFuturesContractById(string contractId, string symbol, DateOnly lastTradeDate) : IBindValue
{
    public object Bind() => new { contractId, symbol, lastTradeDate };
}
internal readonly record struct GetCurrentlyTradeFuturesContract(string symbol) : IBindValue
{
    public object Bind() => new { symbol };
}
internal readonly record struct GetCurrentlyTradeFuturesContracts(string symbol) : IBindValue
{
    public object Bind() => new { symbol };
}
internal readonly record struct GetFuturesContract(string contractId) : IBindValue
{
    public object Bind() => new { contractId };
}
internal readonly record struct GetFuturesContractById(string contractId, string symbol, DateOnly lastTradeDate) : IBindValue
{
    public object Bind() => new { contractId, symbol, lastTradeDate };
}
internal readonly record struct GetFuturesContractsByIds(ICollection<string> contractIds, string symbol) : IBindValue
{
    public object Bind() => new { contractIds, symbol };
}
internal readonly record struct GetFuturesContractsBySymbol(string symbol) : IBindValue
{
    public object Bind() => new { symbol };
}
internal readonly record struct InsertFuturesOptionContract(string contractId, string description, string symbol, string localSymbol, string securityType, string currency, string exchange, string multiplier, DateOnly contractMonth, double strikePrice, string optionType) : IBindValue
{
    public object Bind() => new { contractId, description, symbol, localSymbol, securityType, currency, exchange, multiplier, contractMonth, strikePrice, optionType };
}
internal readonly record struct DeleteFuturesOptionContract(string contractId) : IBindValue
{
    public object Bind() => new { contractId };
}
internal readonly record struct GetFuturesOptionContract(string contractId) : IBindValue
{
    public object Bind() => new { contractId };
}
