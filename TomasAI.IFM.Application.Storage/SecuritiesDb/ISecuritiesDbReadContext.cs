using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

/// <summary>
/// Defines read-only operations for retrieving futures and futures option contract data from the securities database.
/// </summary>
/// <remarks>This interface provides asynchronous methods to query currently traded and historical futures
/// contracts, as well as futures option contracts, by symbol, contract identifier, or in bulk. Implementations are
/// expected to provide efficient, read-only access to contract information for use in trading, analytics, or reporting
/// scenarios.</remarks>
public interface ISecuritiesDbReadContext : ICurrentFuturesContractCatalog, IOptionPricingConventionStore
{
    /// <inheritdoc />
    new Task<OptionPricingConvention?> GetAsync(
        string contractId,
        string mappingVersion,
        CancellationToken cancellationToken);
    Task<ReferenceContractVersion?> GetReferenceVersionAsync(
        string contractId,
        string version,
        CancellationToken cancellationToken = default);
    Task<bool> ContainsReferenceVersionAsync(
        string contractId,
        string version,
        CancellationToken cancellationToken = default);
    Task<ReferenceContractVersion?> GetEffectiveReferenceVersionAsync(
        string contractId,
        string version,
        DateTimeOffset effectiveAtUtc,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReferenceContractVersionSummaryReadModel>> ListReferenceVersionsAsync(
        string contractId,
        string afterVersion = "",
        int limit = 100,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OptionContractExpiryReadModel>> GetOptionContractExpiriesAsync(
        string symbol, DateOnly fromExpiry, DateOnly throughExpiry,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CachedOptionContractDefinitionReadModel>> GetCachedOptionContractDefinitionsAsync(
        string symbol, string underlyingContractId, DateOnly expiryDate,
        IReadOnlyCollection<string>? providerRoots = null,
        CancellationToken cancellationToken = default);
    Task<FuturesContractV3ReadModel?> GetOnTheRunFuturesContractAsync(string symbol);
    Task<FuturesContractV3ReadModel?> GetOnTheRunFuturesContractAsync(string symbol, CancellationToken cancellationToken);
    Task<ICollection<FuturesContractV3ReadModel>> GetRolloverFuturesContractsAsync(string symbol);
    Task<ICollection<FuturesContractV3ReadModel>> GetRolloverFuturesContractsAsync(string symbol, CancellationToken cancellationToken);
    Task<FuturesContractV3ReadModel?> GetFuturesContractAsync(string contractId);
    Task<FuturesContractV3ReadModel?> GetFuturesContractAsync(string contractId, CancellationToken cancellationToken);
    Task<FuturesContractV3ReadModel?> GetFuturesContractAsync(FuturesContractId contractId);
    Task<FuturesContractV3ReadModel?> GetFuturesContractAsync(FuturesContractId contractId, CancellationToken cancellationToken);
    Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsAsync();
    Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsAsync(CancellationToken cancellationToken);
    Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsBySymbolAsync(string symbol);
    Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsBySymbolAsync(string symbol, CancellationToken cancellationToken);
    Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsByIdsAsync(ICollection<string> contractIds, string symbol);
    Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsByIdsAsync(ICollection<string> contractIds, string symbol, CancellationToken cancellationToken);
    Task<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(string contractId);
    Task<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(string contractId, CancellationToken cancellationToken);
    Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsByIdsAsync(ICollection<string> contractIds);
    Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsByIdsAsync(ICollection<string> contractIds, CancellationToken cancellationToken);
    Task<FuturesOptionContractPageReadModel> GetFuturesOptionContractsPageAsync(
        GetFuturesOptionContractsPageParameter request,
        CancellationToken cancellationToken = default);
    Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync(string symbol);
    Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync(string symbol, CancellationToken cancellationToken);
    Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync();
    Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync(CancellationToken cancellationToken);
}
