using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Shared.MarketData.Definitions;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;
/// <summary>Defines Reference database commands.</summary>
public interface IReferenceDbWriteContext
{
    /// <summary>Backfills Reference query projections from canonical storage.</summary>
    /// <param name = "batchSize">The maximum batch size.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <param name = "staleOperationCutoffUtc">The optional stale-operation cutoff.</param>
    /// <returns>The backfill result.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    /// <summary>Reconciles Reference query projections with canonical storage.</summary>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The reconciliation result.</returns>
    Task<ReferenceProjectionReconciliationResult> ReconcileQueryProjectionsV2Async(CancellationToken cancellationToken = default);
    /// <summary>Deletes a lookup value.</summary>
    /// <param name = "lookupTypeId">The lookup identity.</param>
    /// <returns>A task representing the command.</returns>
    Task DeleteLookupTypeAsync(LookupTypeId lookupTypeId);
    /// <summary>Deletes a scheduled job.</summary>
    /// <param name = "jobId">The scheduled-job identifier.</param>
    /// <returns>A task representing the command.</returns>
    Task DeleteScheduledJobAsync(int jobId);
    /// <summary>Deletes MDI forward-loss ratios.</summary>
    /// <param name = "trendDirection">The intrinsic-time trend direction.</param>
    /// <param name = "tradeType">The trade type.</param>
    /// <returns>A task representing the command.</returns>
    Task DeleteMDIForwardLossRatioAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType);
    /// <summary>Inserts a lookup value.</summary>
    /// <param name = "lookupType">The lookup value.</param>
    /// <returns>A task representing the command.</returns>
    Task InsertLookupTypeAsync(LookupTypeReadModel lookupType);
    /// <summary>Inserts a scheduled job.</summary>
    /// <param name = "scheduledJob">The scheduled job.</param>
    /// <returns>A task representing the command.</returns>
    Task InsertScheduledJobAsync(ScheduledJobReadModel scheduledJob);
    /// <summary>Inserts one MDI forward-loss ratio.</summary>
    /// <param name = "mdiForwardLossRatio">The ratio.</param>
    /// <returns>A task representing the command.</returns>
    Task InsertMDIForwardLossRatioAsync(MDIForwardLossRatioReadModel mdiForwardLossRatio);
    /// <summary>Inserts multiple MDI forward-loss ratios.</summary>
    /// <param name = "mdiForwardLossRatios">The ratios.</param>
    /// <returns>A task representing the command.</returns>
    Task InsertMDIForwardLossRatiosAsync(ICollection<MDIForwardLossRatioReadModel> mdiForwardLossRatios);
    /// <summary>Updates a scheduled job.</summary>
    /// <param name = "scheduledJob">The scheduled job.</param>
    /// <returns>A task representing the command.</returns>
    Task UpdateScheduledJobAsync(ScheduledJobReadModel scheduledJob);
    /// <summary>Updates a lookup value.</summary>
    /// <param name = "id">The existing lookup identity.</param>
    /// <param name = "lookupType">The replacement lookup value.</param>
    /// <returns>A task representing the command.</returns>
    Task UpdateLookupTypeAsync(LookupTypeId id, LookupTypeReadModel lookupType);
    /// <summary>Updates an MDI forward-loss ratio.</summary>
    /// <param name = "mdiForwardLossRatio">The ratio.</param>
    /// <returns>A task representing the command.</returns>
    Task UpdateMDIForwardLossRatioAsync(MDIForwardLossRatioReadModel mdiForwardLossRatio);
    /// <summary>Inserts a trade-strategy-family definition.</summary>
    /// <param name = "family">The family definition.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the command.</returns>
    Task InsertTradeStrategyFamilyAsync(TradeStrategyFamilyReadModel family, CancellationToken cancellationToken = default);
    /// <summary>Persists an exact instrument definition in the supplied snapshot bucket.</summary>
    /// <param name="snapshot">The snapshot supplied by the caller.</param>
    /// <param name="index">The index supplied by the caller.</param>
    /// <param name="row">The row supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertInstrumentDefinitionAsync(Guid snapshot, long index, ExactInstrumentDefinition row, CancellationToken cancellationToken);
    /// <summary>Persists up to 128 exact definitions in one bounded PostgreSQL statement.</summary>
    /// <param name="snapshot">The snapshot supplied by the caller.</param>
    /// <param name="firstIndex">The first index supplied by the caller.</param>
    /// <param name="rows">The rows supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertInstrumentDefinitionsAsync(Guid snapshot, long firstIndex, IReadOnlyList<ExactInstrumentDefinition> rows, CancellationToken cancellationToken);
    /// <summary>Persists the binary instrument selection index entry.</summary>
    /// <param name="value">The accepted value supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertInstrumentDefinitionSelectionAsync(InstrumentDefinitionSelection value, CancellationToken cancellationToken);
    /// <summary>Stores a stable product identity in a definition snapshot.</summary>
    /// <param name="snapshot">The snapshot supplied by the caller.</param>
    /// <param name="family">The family supplied by the caller.</param>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertInstrumentDefinitionProductAsync(Guid snapshot, TradeStrategyFamilyType family, TradeStrategySymbolReadModel symbol, CancellationToken cancellationToken = default);
    /// <summary>Serializes modifications to the caller-selected catalog stream inside the transaction.</summary>
    /// <param name="transaction">The caller-owned PostgreSQL transaction.</param>
    /// <param name="stream">The catalog stream used as the advisory lock key.</param>
    /// <param name="token">Cancels lock acquisition.</param>
    /// <returns>Completion after the transaction owns the lock.</returns>
    Task LockInstrumentCatalogAsync(IEnlistedPostgresTransaction transaction, string stream, CancellationToken token);

    /// <summary>Marks the supplied selection complete and writes its exact publication pointer.</summary>
    /// <param name="transaction">The caller-owned PostgreSQL transaction.</param>
    /// <param name="snapshot">The accepted snapshot selected by the owner.</param>
    /// <param name="token">Cancels the writes.</param>
    /// <returns>Completion after both projection writes.</returns>
    Task WriteInstrumentCatalogPointerAsync(IEnlistedPostgresTransaction transaction, InstrumentDefinitionSnapshot snapshot, CancellationToken token);
    /// <summary>Inserts a supplied product identity without replacing an existing natural key.</summary>
    /// <param name="product">The product supplied by the caller.</param>
    /// <param name="id">The id supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertTradeStrategyProductAsync(TradeStrategyProduct product, int id, CancellationToken cancellationToken = default);
    /// <summary>Initializes a supplied catalog document if no catalog exists.</summary>
    /// <param name="revision">The revision supplied by the caller.</param>
    /// <param name="payload">The payload supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InitializeTradeStrategyFamilyCatalogAsync(long revision, string payload, CancellationToken cancellationToken = default);
    /// <summary>Conditionally replaces a catalog document at the supplied expected revision.</summary>
    /// <param name="revision">The revision supplied by the caller.</param>
    /// <param name="payload">The payload supplied by the caller.</param>
    /// <param name="expectedRevision">The expected revision supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task UpdateTradeStrategyFamilyCatalogAsync(long revision, string payload, long expectedRevision, CancellationToken cancellationToken = default);
}
