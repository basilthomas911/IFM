using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Application.MarketData.Contracts;

using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;
/// <summary>Defines Reference database queries.</summary>
public interface IReferenceDbReadContext
{
    /// <summary>Gets the current value of the named seed allocator.</summary>
    /// <param name = "seedType">The seed type.</param>
    /// <returns>The current seed identifier.</returns>
    Task<int> GetCurrentSeedIdAsync(string seedType);
    /// <summary>Gets the current value of the named seed allocator.</summary>
    /// <param name = "seedType">The seed type.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The current seed identifier.</returns>
    Task<int> GetCurrentSeedIdAsync(string seedType, CancellationToken cancellationToken);
    /// <summary>Gets one lookup value by identity.</summary>
    /// <param name = "lookupTypeId">The lookup identity.</param>
    /// <returns>The matching lookup value, or <see langword="null"/>.</returns>
    Task<LookupTypeReadModel?> GetLookupTypeAsync(LookupTypeId lookupTypeId);
    /// <summary>Gets one lookup value by identity.</summary>
    /// <param name = "lookupTypeId">The lookup identity.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The matching lookup value, or <see langword="null"/>.</returns>
    Task<LookupTypeReadModel?> GetLookupTypeAsync(LookupTypeId lookupTypeId, CancellationToken cancellationToken);
    /// <summary>Gets all values for a lookup type.</summary>
    /// <param name = "lookupTypeName">The lookup type name.</param>
    /// <returns>The matching lookup values.</returns>
    Task<ICollection<LookupTypeReadModel>> GetLookupTypeAsync(string lookupTypeName);
    /// <summary>Gets all values for a lookup type.</summary>
    /// <param name = "lookupTypeName">The lookup type name.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The matching lookup values.</returns>
    Task<ICollection<LookupTypeReadModel>> GetLookupTypeAsync(string lookupTypeName, CancellationToken cancellationToken);
    /// <summary>Gets every lookup value.</summary>
    /// <returns>All lookup values.</returns>
    Task<ICollection<LookupTypeReadModel>> GetLookupTypesAsync();
    /// <summary>Gets every lookup value.</summary>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>All lookup values.</returns>
    Task<ICollection<LookupTypeReadModel>> GetLookupTypesAsync(CancellationToken cancellationToken);
    /// <summary>Gets the distinct lookup type names.</summary>
    /// <returns>The lookup type names.</returns>
    Task<ICollection<string>> GetLookupTypeNamesAsync();
    /// <summary>Gets the distinct lookup type names.</summary>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The lookup type names.</returns>
    Task<ICollection<string>> GetLookupTypeNamesAsync(CancellationToken cancellationToken);
    /// <summary>Gets the short codes for a lookup type.</summary>
    /// <param name = "lookupTypeName">The lookup type name.</param>
    /// <returns>The matching short codes.</returns>
    Task<ICollection<LookupTypeShortCodeReadModel>> GetLookupTypeShortCodesAsync(string lookupTypeName);
    /// <summary>Gets the short codes for a lookup type.</summary>
    /// <param name = "lookupTypeName">The lookup type name.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The matching short codes.</returns>
    Task<ICollection<LookupTypeShortCodeReadModel>> GetLookupTypeShortCodesAsync(string lookupTypeName, CancellationToken cancellationToken);
    /// <summary>Determines whether a lookup short code exists.</summary>
    /// <param name = "lookupTypeName">The lookup type name.</param>
    /// <param name = "shortCode">The short code.</param>
    /// <returns><see langword="true"/> when the short code exists.</returns>
    Task<bool> LookupTypeShortCodeExistsAsync(string lookupTypeName, string shortCode);
    /// <summary>Determines whether a lookup short code exists.</summary>
    /// <param name = "lookupTypeName">The lookup type name.</param>
    /// <param name = "shortCode">The short code.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the short code exists.</returns>
    Task<bool> LookupTypeShortCodeExistsAsync(string lookupTypeName, string shortCode, CancellationToken cancellationToken);
    /// <summary>Gets all scheduled jobs.</summary>
    /// <returns>The scheduled jobs.</returns>
    Task<ICollection<ScheduledJobReadModel>> GetScheduledJobsAsync();
    /// <summary>Gets all scheduled jobs.</summary>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The scheduled jobs.</returns>
    Task<ICollection<ScheduledJobReadModel>> GetScheduledJobsAsync(CancellationToken cancellationToken);
    /// <summary>Gets the identifier reserved for a scheduled-job name.</summary>
    /// <param name = "jobName">The scheduled-job name.</param>
    /// <returns>The scheduled-job identifier.</returns>
    Task<int> GetScheduledJobIdAsync(string jobName);
    /// <summary>Gets the identifier reserved for a scheduled-job name.</summary>
    /// <param name = "jobName">The scheduled-job name.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The scheduled-job identifier.</returns>
    Task<int> GetScheduledJobIdAsync(string jobName, CancellationToken cancellationToken);
    /// <summary>Gets MDI forward-loss ratios.</summary>
    /// <param name = "trendDirection">The intrinsic-time trend direction.</param>
    /// <param name = "tradeType">The trade type.</param>
    /// <returns>The matching ratios.</returns>
    Task<ICollection<MDIForwardLossRatioReadModel>> GetMDIForwardLossRatiosAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType);
    /// <summary>Gets MDI forward-loss ratios.</summary>
    /// <param name = "trendDirection">The intrinsic-time trend direction.</param>
    /// <param name = "tradeType">The trade type.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The matching ratios.</returns>
    Task<ICollection<MDIForwardLossRatioReadModel>> GetMDIForwardLossRatiosAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType, CancellationToken cancellationToken);
    /// <summary>Gets the current trade-strategy-family catalog.</summary>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The current family definitions.</returns>
    Task<IReadOnlyList<TradeStrategyFamilyReadModel>> GetTradeStrategyFamiliesAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets one trade-strategy-family definition.</summary>
    /// <param name = "tradeStrategyFamilyId">The family identifier.</param>
    /// <param name = "definitionVersion">The definition version.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The family definition, or <see langword="null"/>.</returns>
    Task<TradeStrategyFamilyReadModel?> GetTradeStrategyFamilyAsync(int tradeStrategyFamilyId, long definitionVersion, CancellationToken cancellationToken = default);
    /// <summary>Reads the currently published instrument-definition snapshot.</summary>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<InstrumentDefinitionSnapshot?> GetInstrumentDefinitionSnapshotAsync(CancellationToken cancellationToken);
    /// <summary>Reads stable product identities for a snapshot and family.</summary>
    /// <param name="snapshot">The snapshot supplied by the caller.</param>
    /// <param name="family">The family supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<TradeStrategySymbolReadModel[]> GetInstrumentDefinitionSymbolsAsync(Guid snapshot, TradeStrategyFamilyType family, CancellationToken cancellationToken);
    /// <summary>Reads unambiguous exchange-sourced descriptions keyed by the provider contract symbol.</summary>
    /// <param name="exchange">The exchange supplied by the caller.</param>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<IReadOnlyDictionary<string,string>> GetContractDescriptionsAsync(string exchange, string symbol, CancellationToken cancellationToken = default);
    /// <summary>Streams the original provider JSON in record-index order.</summary>
    /// <param name="snapshot">The snapshot supplied by the caller.</param>
    /// <param name="dataset">The Databento dataset identity.</param>
    /// <param name="bucket">The bucket supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>An asynchronous stream of the selected rows.</returns>
    IAsyncEnumerable<string> ReadInstrumentDefinitionJsonAsync(Guid snapshot, string dataset, int bucket, CancellationToken cancellationToken = default);
    /// <summary>Reads one exact provider record by its immutable provenance key.</summary>
    /// <param name="snapshot">The snapshot supplied by the caller.</param>
    /// <param name="dataset">The Databento dataset identity.</param>
    /// <param name="bucket">The bucket supplied by the caller.</param>
    /// <param name="index">The index supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<string?> GetInstrumentDefinitionJsonAsync(Guid snapshot, string dataset, int bucket, long index, CancellationToken cancellationToken = default);
    /// <summary>Reads whether the supplied snapshot has a completed selector index.</summary>
    /// <param name="snapshot">The snapshot supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>True when the requested condition holds.</returns>
    Task<bool> IsInstrumentDefinitionSelectionCompleteAsync(Guid snapshot, CancellationToken cancellationToken = default);
    /// <summary>Reads one ordered, bounded selector page after the supplied key.</summary>
    /// <param name="snapshot">The snapshot supplied by the caller.</param>
    /// <param name="dataset">The Databento dataset identity.</param>
    /// <param name="root">The root supplied by the caller.</param>
    /// <param name="options">The options supplied by the caller.</param>
    /// <param name="after">The after supplied by the caller.</param>
    /// <param name="limit">The maximum number of returned rows.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<IReadOnlyList<InstrumentDefinitionSelectionRow>> GetInstrumentDefinitionSelectionAsync(Guid snapshot, string dataset, string root, bool options, string after, int limit, CancellationToken cancellationToken = default);
    /// <summary>Reads a stable product identity by family, exchange, symbol and currency.</summary>
    /// <param name="product">The product supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<TradeStrategySymbolReadModel?> GetTradeStrategyProductAsync(TradeStrategyProduct product, CancellationToken cancellationToken = default);
    /// <summary>Reads a stable symbol by family and identifier.</summary>
    /// <param name="family">The family supplied by the caller.</param>
    /// <param name="id">The id supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<TradeStrategySymbolReadModel?> GetTradeStrategySymbolAsync(TradeStrategyFamilyType family, int id, CancellationToken cancellationToken = default);
    /// <summary>Reads the persisted catalog revision and document.</summary>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<TradeStrategyFamilyCatalogReadModel?> GetTradeStrategyFamilyCatalogAsync(CancellationToken cancellationToken = default);
    /// <summary>Reads the exact committed catalog revision without allocating a stream.</summary>
    /// <param name="transaction">The caller-owned PostgreSQL transaction.</param>
    /// <param name="stream">The catalog stream identity.</param>
    /// <param name="token">Cancels the read.</param>
    /// <returns>The committed revision, or zero if absent.</returns>
    Task<long> ReadInstrumentCatalogRevisionAsync(IEnlistedPostgresTransaction transaction, string stream, CancellationToken token);

    /// <summary>Reads the exact raw event row for the caller-selected revision.</summary>
    /// <param name="transaction">The caller-owned PostgreSQL transaction.</param>
    /// <param name="stream">The catalog stream identity.</param>
    /// <param name="revision">The exact source revision.</param>
    /// <param name="token">Cancels the read.</param>
    /// <returns>The event row or null if absent.</returns>
    Task<EventStreamReadModel?> ReadInstrumentCatalogEventAsync(IEnlistedPostgresTransaction transaction, string stream, long revision, CancellationToken token);

    /// <summary>Reads the completed timestamp from the current projection pointer.</summary>
    /// <param name="transaction">The caller-owned PostgreSQL transaction.</param>
    /// <param name="token">Cancels the read.</param>
    /// <returns>The projected completion time, or null if absent.</returns>
    Task<DateTime?> ReadPreviousInstrumentCompletionAsync(IEnlistedPostgresTransaction transaction, CancellationToken token);

    /// <summary>Checks for the supplied catalog publication command identity.</summary>
    /// <param name="transaction">The caller-owned PostgreSQL transaction.</param>
    /// <param name="stream">The exact catalog stream identity.</param>
    /// <param name="commandId">The previously published command identity.</param>
    /// <param name="token">Cancels the lookup.</param>
    /// <returns>True when the stream contains the command identity.</returns>
    Task<bool> HasInstrumentCatalogReceiptAsync(IEnlistedPostgresTransaction transaction, string stream, Guid commandId, CancellationToken token);

    /// <summary>Checks whether the requested selection contains staged rows.</summary>
    /// <param name="transaction">The caller-owned PostgreSQL transaction.</param>
    /// <param name="snapshot">The exact staged snapshot identity.</param>
    /// <param name="token">Cancels the lookup.</param>
    /// <returns>True when staged selection rows exist.</returns>
    Task<bool> HasInstrumentDefinitionSelectionAsync(IEnlistedPostgresTransaction transaction, Guid snapshot, CancellationToken token);

}
