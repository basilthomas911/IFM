using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Shared.Storage.BindValues;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

// Provider-neutral positional binding models. Keep each binding body unchanged when relocating it.

internal readonly record struct InsertOptionDefinitionGenerationParameters(string Symbol, Guid Generation,
    IReadOnlyList<(DateOnly Expiry, string Root, string ContractId, string UnderlyingId, string Family, byte[] Payload)> Rows,
    DateTime RefreshedAtUtc) : IBindValue
{
    public object Bind()
    {
        var count = Rows.Count;
        var expiries = new DateOnly[count];
        var roots = new string[count];
        var contracts = new string[count];
        var underlyings = new string[count];
        var families = new string[count];
        var payloads = new byte[count][];
        for (var index = 0; index < count; index++)
        {
            var row = Rows[index];
            expiries[index] = row.Expiry;
            roots[index] = row.Root;
            contracts[index] = row.ContractId;
            underlyings[index] = row.UnderlyingId;
            families[index] = row.Family;
            payloads[index] = row.Payload;
        }
        return Values(Symbol, Generation, expiries, roots, contracts, underlyings, families, payloads, RefreshedAtUtc);
    }
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct BeginSecuritiesProjectionOperationV3(Guid generation, HashSet<Guid> activeOperations, string projectionName) : IBindValue
{
    public object Bind() => new object?[] { generation, activeOperations, projectionName };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct BeginSecuritiesSymbolProjectionOperationV3(Guid generation, HashSet<Guid> activeOperations, string projectionName, string symbol) : IBindValue
{
    public object Bind() => new object?[] { generation, activeOperations, projectionName, symbol };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct ClaimReferenceIdentity(string key, string binding) : IBindValue
{
    public object Bind() => new object?[] { key, binding };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct CommitReferenceVersion(string id, string version, string digest) : IBindValue
{
    public object Bind() => new object?[] { id, version, digest };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct CompleteSecuritiesProjectionOperationV3(HashSet<Guid> activeOperations, string projectionName, Guid generation, HashSet<Guid> expectedActiveOperations) : IBindValue
{
    public object Bind() => new object?[] { activeOperations, projectionName, generation, expectedActiveOperations };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct CompleteSecuritiesSymbolProjectionOperationV3(HashSet<Guid> activeOperations, string projectionName, string symbol, Guid generation, HashSet<Guid> expectedActiveOperations) : IBindValue
{
    public object Bind() => new object?[] { activeOperations, projectionName, symbol, generation, expectedActiveOperations };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesContract(string contractId) : IBindValue
{
    public object Bind() => new object?[] { Text(contractId) };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesContractById(string contractId, string symbol, DateOnly lastTradeDate) : IBindValue
{
    public object Bind() => new object?[] { Text(contractId), Text(symbol), Date(lastTradeDate) };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesContractBySymbolV3(string symbol, bool rollover, bool onTheRun, DateOnly lastTradeDate, string contractId) : IBindValue
{
    public object Bind() => new object?[] { Text(symbol), Boolean(rollover), Boolean(onTheRun), Date(lastTradeDate), Text(contractId) };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesContractBySymbolV3Partition(string symbol) : IBindValue
{
    public object Bind() => new object?[] { Text(symbol) };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesContractRollover(string symbol) : IBindValue
{
    public object Bind() => new object?[] { symbol };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesOptionContract(string contractId) : IBindValue
{
    public object Bind() => new object?[] { Text(contractId) };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesOptionContractById(string contractId, DateOnly contractMonth, string symbol, string optionType, double strikePrice) : IBindValue
{
    public object Bind() => new object?[] { Text(contractId), Date(contractMonth), Text(symbol), Text(optionType), Double(strikePrice) };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesOptionContractBySymbolV2(string symbol, DateOnly contractMonth, string optionType, double strikePrice, string contractId) : IBindValue
{
    public object Bind() => new object?[] { Text(symbol), Date(contractMonth), Text(optionType), Double(strikePrice), Text(contractId) };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesOptionContractBySymbolV2Partition(string symbol) : IBindValue
{
    public object Bind() => new object?[] { Text(symbol) };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteOptionContractExpiryGeneration(string symbol, Guid generation) : IBindValue
{
    public object Bind() => new object?[] { symbol, generation };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteSecuritiesProjectionOperationScopesV3(string projectionName, Guid operationId) : IBindValue
{
    public object Bind() => new object?[] { projectionName, operationId };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteSecuritiesProjectionOperationV3(string projectionName, Guid operationId) : IBindValue
{
    public object Bind() => new object?[] { projectionName, operationId };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteSecuritiesProjectionStateV3(string projectionName) : IBindValue
{
    public object Bind() => new object?[] { projectionName };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteSecuritiesSymbolProjectionStateV3(string projectionName, string symbol) : IBindValue
{
    public object Bind() => new object?[] { projectionName, symbol };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct EndSecuritiesProjectionOperationV3(Guid generation, HashSet<Guid> activeOperations, string projectionName) : IBindValue
{
    public object Bind() => new object?[] { generation, activeOperations, projectionName };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct EndSecuritiesSymbolProjectionOperationV3(Guid generation, HashSet<Guid> activeOperations, string projectionName, string symbol) : IBindValue
{
    public object Bind() => new object?[] { generation, activeOperations, projectionName, symbol };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetCachedOptionContractDefinitions(
    string symbol, Guid generation, DateOnly expiryDate) : IBindValue
{
    public object Bind() => Values(Text(symbol),Uuid(generation),Date(expiryDate));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetCachedOptionDefinitionPartitions(
    string symbol, Guid generation, DateOnly[] expiries) : IBindValue
{
    public object Bind() => Values(Text(symbol), Uuid(generation), expiries);
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesContract(string contractId) : IBindValue
{
    public object Bind() => new object?[] { Text(contractId) };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesContractById(string contractId, string symbol, DateOnly lastTradeDate) : IBindValue
{
    public object Bind() => new object?[] { Text(contractId), Text(symbol), Date(lastTradeDate) };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesContractRollover(string symbol) : IBindValue
{
    public object Bind() => new object?[] { Text(symbol) };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesContractsByIds(ICollection<string> contractIds, string symbol) : IBindValue
{
    public object Bind() => new object?[] { TextArray([.. contractIds] ), Text(symbol) };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesContractsBySymbol(string symbol) : IBindValue
{
    public object Bind() => new object?[] { Text(symbol) };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesOptionContract(string contractId) : IBindValue
{
    public object Bind() => new object?[] { Text(contractId) };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesOptionContractsByIds(ICollection<string> contractIds) : IBindValue
{
    public object Bind() => new object?[] { TextArray([.. contractIds] ) };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesOptionContractsBySymbol(string symbol) : IBindValue
{
    public object Bind() => new object?[] { Text(symbol) };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetOnTheRunFuturesContract(string symbol) : IBindValue
{
    public object Bind() => new object?[] { Text(symbol) };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetOptionContractExpiries(
    string symbol, Guid generation, DateOnly fromExpiry, DateOnly throughExpiry) : IBindValue
{
    public object Bind() => Values(Text(symbol),Uuid(generation),Date(fromExpiry),Date(throughExpiry));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetOptionContractExpiryCalendarState(string symbol) : IBindValue
{
    public object Bind() => Values(Text(symbol));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetOptionPricingReferenceBundle(string id) : IBindValue
{
    public object Bind() => new object[] { id };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetReferenceIdentityClaim(string key) : IBindValue
{
    public object Bind() => new object?[] { key };
}

/// <summary>Binds one bounded JSON key batch using provider-neutral C# values.</summary>
internal readonly record struct GetReferenceIdentityClaims(string Json) : IBindValue
{
    public object Bind() => new object?[] { Text(Json) };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetReferenceVersion(string id, string version) : IBindValue
{
    public object Bind() => new object?[] { id, version };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetReferenceVersionHistory(string id, string after, int pageLimit) : IBindValue
{
    public object Bind() => new object?[] { id, after, pageLimit };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetRolloverFuturesContracts(string symbol) : IBindValue
{
    public object Bind() => new object?[] { Text(symbol) };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetSecuritiesProjectionOperationScopesV3(string projectionName, Guid operationId) : IBindValue
{
    public object Bind() => new object?[] { projectionName, operationId };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetSecuritiesProjectionOperationsV3(string projectionName) : IBindValue
{
    public object Bind() => new object?[] { projectionName };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetSecuritiesProjectionStateV3(string projectionName) : IBindValue
{
    public object Bind() => new object?[] { projectionName };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetSecuritiesSymbolProjectionStatesV3(string projectionName, ICollection<string> symbols) : IBindValue
{
    public object Bind() => new object?[] { projectionName, symbols };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetSecuritiesSymbolProjectionStateV3(string projectionName, string symbol) : IBindValue
{
    public object Bind() => new object?[] { projectionName, symbol };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesContract(string contractId, string description, string symbol, string localSymbol, string securityType, string currency, string exchange, string multiplier, DateOnly lastTradeDate, bool onTheRun, bool rollover, byte[]? referencePayload = null) : IBindValue
{
    public object Bind() => new object?[] { Text(contractId), Text(description), Text(symbol), Text(localSymbol), Text(securityType), Text(currency), Text(exchange), Text(multiplier), Date(lastTradeDate), Boolean(onTheRun), Boolean(rollover), Bytea(referencePayload) };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesContractRolloverIfMissing(
    string symbol,
    DateTime createdOn,
    string createdBy) : IBindValue
{
    public object Bind() => new object?[] { Text(symbol), TimestampTz(createdOn), Text(createdBy) };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesOptionContract(string contractId, string description, string symbol, string localSymbol, string securityType, string currency, string exchange, string multiplier, DateOnly contractMonth, double strikePrice, string optionType, byte[]? referencePayload = null) : IBindValue
{
    public object Bind() => new object?[] { Text(contractId), Text(description), Text(symbol), Text(localSymbol), Text(securityType), Text(currency), Text(exchange), Text(multiplier), Date(contractMonth), Double(strikePrice), Text(optionType), Bytea(referencePayload) };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertOptionContractExpiry(
    string symbol, Guid generation, DateOnly expiryDate, string providerRoot,
    string contractId, string underlyingContractId, string optionFamily,
    byte[] definitionPayload, DateTime refreshedAtUtc) : IBindValue
{
    public object Bind() => new object?[]
        { symbol, generation, expiryDate, providerRoot, contractId, underlyingContractId,
            optionFamily, definitionPayload, refreshedAtUtc };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertOptionPricingReferenceBundle(string id, string payload) : IBindValue
{
    public object Bind() => new object[] { id, payload };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertSecuritiesProjectionOperationScopeV3(string projectionName, Guid operationId, string scopeType, string scopeKey) : IBindValue
{
    public object Bind() => new object?[] { projectionName, operationId, scopeType, scopeKey };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertSecuritiesProjectionOperationV3(string projectionName, Guid operationId, DateTime startedOn) : IBindValue
{
    public object Bind() => new object?[] { projectionName, operationId, startedOn };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InvalidateSecuritiesProjectionStateV3(Guid generation, string projectionName) : IBindValue
{
    public object Bind() => new object?[] { generation, projectionName };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct NativeOptionPageParameters(string Symbol, DateOnly? Month, string? OptionType,
    double? Strike, string? ContractId, int Limit) : IBindValue
{
    public object Bind() => new object?[] { Text(Symbol), Date(Month), Text(OptionType), Double(Strike), Text(ContractId), Integer(Limit) };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct PublishOptionContractExpiryGeneration(
    string symbol, Guid generation, DateOnly coverageFrom, DateOnly coverageThrough,
    DateTime refreshedAtUtc) : IBindValue
{
    public object Bind() => new object?[]
        { symbol, generation, coverageFrom, coverageThrough, refreshedAtUtc };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct RemoveSecuritiesProjectionOperationV3(Guid operationId, string projectionName) : IBindValue
{
    public object Bind() => new object?[] { operationId, projectionName };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct RemoveSecuritiesSymbolProjectionOperationV3(Guid operationId, string projectionName, string symbol) : IBindValue
{
    public object Bind() => new object?[] { operationId, projectionName, symbol };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct SetSecuritiesProjectionOperationStateMayBeActiveV3(bool stateMayBeActive, string projectionName, Guid operationId, bool expectedStateMayBeActive) : IBindValue
{
    public object Bind() => new object?[] { stateMayBeActive, projectionName, operationId, expectedStateMayBeActive };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct StageReferenceVersion(string id, string version, string digest, byte[] payload) : IBindValue
{
    public object Bind() => new object?[] { id, version, digest, payload };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpdateFuturesContractRollover(
    string contractId,
    DateOnly nextRolloverDate,
    DateTime updatedOn,
    string updatedBy,
    string symbol) : IBindValue
{
    public object Bind() => new object?[] { Text(contractId), Date(nextRolloverDate), TimestampTz(updatedOn), Text(updatedBy), Text(symbol) };
}

internal readonly record struct CatalogRolloverSeed(string Symbol, DateTime CreatedOnUtc, string CreatedBy) : IBindValue
{
    public object Bind() => Values(Text(Symbol), TimestampTz(CreatedOnUtc), Text(CreatedBy));
}

internal readonly record struct ReadExactReferenceVersions(string[] ContractIds, string[] Versions) : IBindValue
{
    public object Bind() => Values(TextArray(ContractIds), TextArray(Versions));
}
