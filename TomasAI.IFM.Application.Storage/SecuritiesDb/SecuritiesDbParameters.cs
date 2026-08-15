using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

internal readonly record struct InsertFuturesContractRolloverIfMissing(
    string symbol,
    DateTime createdOn,
    string createdBy) : IBindValue
{
    public object Bind() => new object?[] { symbol, createdOn, createdBy };
}

internal readonly record struct GetFuturesContractRollover(string symbol) : IBindValue
{
    public object Bind() => new object?[] { symbol };
}

internal readonly record struct DeleteFuturesContractRollover(string symbol) : IBindValue
{
    public object Bind() => new object?[] { symbol };
}

internal readonly record struct UpdateFuturesContractRollover(
    string contractId,
    DateOnly nextRolloverDate,
    DateTime updatedOn,
    string updatedBy,
    string symbol) : IBindValue
{
    public object Bind() => new object?[]
        { contractId, nextRolloverDate, updatedOn, updatedBy, symbol };
}

internal readonly record struct InsertFuturesContract(string contractId, string description, string symbol, string localSymbol, string securityType, string currency, string exchange, string multiplier, DateOnly lastTradeDate, bool currentlyTraded) : IBindValue
{
    public object Bind() => new object?[] { contractId, description, symbol, localSymbol, securityType, currency, exchange, multiplier, lastTradeDate, currentlyTraded };
}
internal readonly record struct DeleteFuturesContract(string contractId) : IBindValue
{
    public object Bind() => new object?[] { contractId };
}
internal readonly record struct DeleteFuturesContractById(string contractId, string symbol, DateOnly lastTradeDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, symbol, lastTradeDate };
}
internal readonly record struct DeleteFuturesOptionContractById(string contractId, DateOnly contractMonth, string symbol, string optionType, double strikePrice) : IBindValue
{
    public object Bind() => new object?[] { contractId, contractMonth, symbol, optionType, strikePrice };
}
internal readonly record struct DeleteFuturesContractBySymbolV2(string symbol, bool currentlyTraded, DateOnly lastTradeDate, string contractId) : IBindValue
{
    public object Bind() => new object?[] { symbol, currentlyTraded, lastTradeDate, contractId };
}
internal readonly record struct DeleteFuturesContractBySymbolV2Partition(string symbol) : IBindValue
{
    public object Bind() => new object?[] { symbol };
}
internal readonly record struct DeleteSecuritiesProjectionStateV3(string projectionName) : IBindValue
{
    public object Bind() => new object?[] { projectionName };
}
internal readonly record struct DeleteSecuritiesSymbolProjectionStateV3(string projectionName, string symbol) : IBindValue
{
    public object Bind() => new object?[] { projectionName, symbol };
}
internal readonly record struct GetCurrentlyTradeFuturesContract(string symbol) : IBindValue
{
    public object Bind() => new object?[] { symbol };
}
internal readonly record struct GetCurrentlyTradeFuturesContracts(string symbol) : IBindValue
{
    public object Bind() => new object?[] { symbol };
}
internal readonly record struct GetFuturesContract(string contractId) : IBindValue
{
    public object Bind() => new object?[] { contractId };
}
internal readonly record struct GetFuturesContractById(string contractId, string symbol, DateOnly lastTradeDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, symbol, lastTradeDate };
}
internal readonly record struct GetFuturesContractsByIds(ICollection<string> contractIds, string symbol) : IBindValue
{
    public object Bind() => new object?[] { contractIds, symbol };
}
internal readonly record struct GetFuturesContractsBySymbol(string symbol) : IBindValue
{
    public object Bind() => new object?[] { symbol };
}
internal readonly record struct GetSecuritiesProjectionStateV3(string projectionName) : IBindValue
{
    public object Bind() => new object?[] { projectionName };
}
internal readonly record struct GetSecuritiesSymbolProjectionStateV3(string projectionName, string symbol) : IBindValue
{
    public object Bind() => new object?[] { projectionName, symbol };
}
internal readonly record struct GetSecuritiesSymbolProjectionStatesV3(string projectionName, ICollection<string> symbols) : IBindValue
{
    public object Bind() => new object?[] { projectionName, symbols };
}
internal readonly record struct GetSecuritiesProjectionOperationsV3(string projectionName) : IBindValue
{
    public object Bind() => new object?[] { projectionName };
}
internal readonly record struct GetSecuritiesProjectionOperationScopesV3(string projectionName, Guid operationId) : IBindValue
{
    public object Bind() => new object?[] { projectionName, operationId };
}
internal readonly record struct InsertSecuritiesProjectionOperationV3(string projectionName, Guid operationId, DateTime startedOn) : IBindValue
{
    public object Bind() => new object?[] { projectionName, operationId, startedOn };
}
internal readonly record struct SetSecuritiesProjectionOperationStateMayBeActiveV3(bool stateMayBeActive, string projectionName, Guid operationId, bool expectedStateMayBeActive) : IBindValue
{
    public object Bind() => new object?[] { stateMayBeActive, projectionName, operationId, expectedStateMayBeActive };
}
internal readonly record struct InsertSecuritiesProjectionOperationScopeV3(string projectionName, Guid operationId, string scopeType, string scopeKey) : IBindValue
{
    public object Bind() => new object?[] { projectionName, operationId, scopeType, scopeKey };
}
internal readonly record struct DeleteSecuritiesProjectionOperationV3(string projectionName, Guid operationId) : IBindValue
{
    public object Bind() => new object?[] { projectionName, operationId };
}
internal readonly record struct DeleteSecuritiesProjectionOperationScopesV3(string projectionName, Guid operationId) : IBindValue
{
    public object Bind() => new object?[] { projectionName, operationId };
}
internal readonly record struct InvalidateSecuritiesProjectionStateV3(Guid generation, string projectionName) : IBindValue
{
    public object Bind() => new object?[] { generation, projectionName };
}
internal readonly record struct BeginSecuritiesProjectionOperationV3(Guid generation, HashSet<Guid> activeOperations, string projectionName) : IBindValue
{
    public object Bind() => new object?[] { generation, activeOperations, projectionName };
}
internal readonly record struct EndSecuritiesProjectionOperationV3(Guid generation, HashSet<Guid> activeOperations, string projectionName) : IBindValue
{
    public object Bind() => new object?[] { generation, activeOperations, projectionName };
}
internal readonly record struct RemoveSecuritiesProjectionOperationV3(Guid operationId, string projectionName) : IBindValue
{
    public object Bind() => new object?[] { operationId, projectionName };
}
internal readonly record struct CompleteSecuritiesProjectionOperationV3(HashSet<Guid> activeOperations, string projectionName, Guid generation, HashSet<Guid> expectedActiveOperations) : IBindValue
{
    public object Bind() => new object?[] { activeOperations, projectionName, generation, expectedActiveOperations };
}
internal readonly record struct BeginSecuritiesSymbolProjectionOperationV3(Guid generation, HashSet<Guid> activeOperations, string projectionName, string symbol) : IBindValue
{
    public object Bind() => new object?[] { generation, activeOperations, projectionName, symbol };
}
internal readonly record struct EndSecuritiesSymbolProjectionOperationV3(Guid generation, HashSet<Guid> activeOperations, string projectionName, string symbol) : IBindValue
{
    public object Bind() => new object?[] { generation, activeOperations, projectionName, symbol };
}
internal readonly record struct RemoveSecuritiesSymbolProjectionOperationV3(Guid operationId, string projectionName, string symbol) : IBindValue
{
    public object Bind() => new object?[] { operationId, projectionName, symbol };
}
internal readonly record struct CompleteSecuritiesSymbolProjectionOperationV3(HashSet<Guid> activeOperations, string projectionName, string symbol, Guid generation, HashSet<Guid> expectedActiveOperations) : IBindValue
{
    public object Bind() => new object?[] { activeOperations, projectionName, symbol, generation, expectedActiveOperations };
}
internal readonly record struct InsertFuturesOptionContract(string contractId, string description, string symbol, string localSymbol, string securityType, string currency, string exchange, string multiplier, DateOnly contractMonth, double strikePrice, string optionType) : IBindValue
{
    public object Bind() => new object?[] { contractId, description, symbol, localSymbol, securityType, currency, exchange, multiplier, contractMonth, strikePrice, optionType };
}
internal readonly record struct DeleteFuturesOptionContract(string contractId) : IBindValue
{
    public object Bind() => new object?[] { contractId };
}
internal readonly record struct GetFuturesOptionContract(string contractId) : IBindValue
{
    public object Bind() => new object?[] { contractId };
}
internal readonly record struct GetFuturesOptionContractsByIds(ICollection<string> contractIds) : IBindValue
{
    public object Bind() => new object?[] { contractIds };
}
internal readonly record struct GetFuturesOptionContractsBySymbol(string symbol) : IBindValue
{
    public object Bind() => new object?[] { symbol };
}
internal readonly record struct DeleteFuturesOptionContractBySymbolV2(string symbol, DateOnly contractMonth, string optionType, double strikePrice, string contractId) : IBindValue
{
    public object Bind() => new object?[] { symbol, contractMonth, optionType, strikePrice, contractId };
}
internal readonly record struct DeleteFuturesOptionContractBySymbolV2Partition(string symbol) : IBindValue
{
    public object Bind() => new object?[] { symbol };
}
