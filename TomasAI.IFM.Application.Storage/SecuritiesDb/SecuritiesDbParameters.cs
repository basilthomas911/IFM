using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

internal readonly record struct StageReferenceVersion(string id, string version, string digest, byte[] payload) : IBindValue
{
    public object Bind() => new object?[] { id, version, digest, payload };
}

internal readonly record struct ClaimReferenceIdentity(string key, string binding) : IBindValue
{
    public object Bind() => new object?[] { key, binding };
}

internal readonly record struct GetReferenceIdentityClaim(string key) : IBindValue
{
    public object Bind() => new object?[] { key };
}

internal readonly record struct CommitReferenceVersion(string id, string version, string digest) : IBindValue
{
    public object Bind() => new object?[] { id, version, digest };
}

internal readonly record struct GetReferenceVersionHistory(string id, string after, int pageLimit) : IBindValue
{
    public object Bind() => new object?[] { id, after, pageLimit };
}

internal readonly record struct GetReferenceVersion(string id, string version) : IBindValue
{
    public object Bind() => new object?[] { id, version };
}

internal readonly record struct ReferenceVersionStorageRow(
    string Digest,
    bool Published,
    TomasAI.IFM.Domain.Reference.Shared.ViewModels.ReferenceContractVersion Value);

internal readonly record struct FuturesContractProjectionKey(
    string Symbol,
    bool Rollover,
    bool OnTheRun,
    DateOnly LastTradeDate,
    string ContractId);

internal readonly record struct FuturesOptionContractProjectionKey(
    string Symbol,
    DateOnly ContractMonth,
    string OptionType,
    double StrikePrice,
    string ContractId);

internal readonly record struct ProjectionState(Guid Generation, bool IsComplete, bool HasNoActiveOperations);

internal readonly record struct SymbolProjectionState(
    string Symbol,
    Guid Generation,
    bool IsComplete,
    bool HasNoActiveOperations);

internal readonly record struct ProjectionReadStamp(
    string ProjectionName,
    string Symbol,
    Guid Generation,
    bool IsGlobal,
    Guid? GlobalGeneration);

internal readonly record struct ProjectionOperationJournalEntry(
    Guid OperationId,
    DateTime StartedOn,
    bool StateMayBeActive);

internal readonly record struct ProjectionOperationScope(string ScopeType, string ScopeKey);

internal sealed record ProjectionOperation(
    Guid OperationId,
    string ProjectionName,
    bool GlobalWasComplete,
    HashSet<string> CompletedSymbols,
    string[] AffectedSymbols);

internal sealed record ProjectionInventory(
    HashSet<FuturesContractProjectionKey> FuturesContractSourceKeys,
    HashSet<FuturesContractProjectionKey> FuturesContractTargetKeys,
    HashSet<FuturesOptionContractProjectionKey> FuturesOptionContractSourceKeys,
    HashSet<FuturesOptionContractProjectionKey> FuturesOptionContractTargetKeys,
    int FuturesContractSourceRows,
    int FuturesContractTargetRows,
    int FuturesOptionContractSourceRows,
    int FuturesOptionContractTargetRows);

internal sealed record OptionPageCursor(string Symbol, int PageSize, ProjectionReadStamp Stamp, byte[] State);

internal sealed record OptionExpiryCalendarState(
    Guid Generation,
    DateOnly CoverageFrom,
    DateOnly CoverageThrough,
    DateTime RefreshedAtUtc);

internal readonly record struct InsertOptionContractExpiry(
    string symbol, Guid generation, DateOnly expiryDate, string providerRoot,
    string contractId, string underlyingContractId, string optionFamily,
    byte[] definitionPayload, DateTime refreshedAtUtc) : IBindValue
{
    public object Bind() => new object?[]
        { symbol, generation, expiryDate, providerRoot, contractId, underlyingContractId,
            optionFamily, definitionPayload, refreshedAtUtc };
}

internal readonly record struct PublishOptionContractExpiryGeneration(
    string symbol, Guid generation, DateOnly coverageFrom, DateOnly coverageThrough,
    DateTime refreshedAtUtc) : IBindValue
{
    public object Bind() => new object?[]
        { symbol, generation, coverageFrom, coverageThrough, refreshedAtUtc };
}

internal readonly record struct GetOptionContractExpiryCalendarState(string symbol) : IBindValue
{
    public object Bind() => new object?[] { symbol };
}

internal readonly record struct GetOptionContractExpiries(
    string symbol, Guid generation, DateOnly fromExpiry, DateOnly throughExpiry) : IBindValue
{
    public object Bind() => new object?[] { symbol, generation, fromExpiry, throughExpiry };
}

internal readonly record struct GetCachedOptionContractDefinitions(
    string symbol, Guid generation, DateOnly expiryDate) : IBindValue
{
    public object Bind() => new object?[] { symbol, generation, expiryDate };
}

internal readonly record struct DeleteOptionContractExpiryGeneration(string symbol, Guid generation) : IBindValue
{
    public object Bind() => new object?[] { symbol, generation };
}

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

internal readonly record struct InsertFuturesContract(string contractId, string description, string symbol, string localSymbol, string securityType, string currency, string exchange, string multiplier, DateOnly lastTradeDate, bool onTheRun, bool rollover, byte[]? referencePayload = null) : IBindValue
{
    public object Bind() => new object?[] { contractId, description, symbol, localSymbol, securityType, currency, exchange, multiplier, lastTradeDate, onTheRun, rollover, referencePayload };
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
internal readonly record struct DeleteFuturesContractBySymbolV3(string symbol, bool rollover, bool onTheRun, DateOnly lastTradeDate, string contractId) : IBindValue
{
    public object Bind() => new object?[] { symbol, rollover, onTheRun, lastTradeDate, contractId };
}
internal readonly record struct DeleteFuturesContractBySymbolV3Partition(string symbol) : IBindValue
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
internal readonly record struct GetOnTheRunFuturesContract(string symbol) : IBindValue
{
    public object Bind() => new object?[] { symbol };
}
internal readonly record struct GetRolloverFuturesContracts(string symbol) : IBindValue
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
internal readonly record struct InsertFuturesOptionContract(string contractId, string description, string symbol, string localSymbol, string securityType, string currency, string exchange, string multiplier, DateOnly contractMonth, double strikePrice, string optionType, byte[]? referencePayload = null) : IBindValue
{
    public object Bind() => new object?[] { contractId, description, symbol, localSymbol, securityType, currency, exchange, multiplier, contractMonth, strikePrice, optionType, referencePayload };
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
