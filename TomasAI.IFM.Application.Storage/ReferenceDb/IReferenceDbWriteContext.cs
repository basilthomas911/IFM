using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;

/// <summary>Defines Reference database commands.</summary>
public interface IReferenceDbWriteContext
{
    /// <summary>Backfills Reference query projections from canonical storage.</summary>
    /// <param name="batchSize">The maximum batch size.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="staleOperationCutoffUtc">The optional stale-operation cutoff.</param>
    /// <returns>The backfill result.</returns>
    Task<ReferenceProjectionBackfillResult> BackfillQueryProjectionsV2Async(
        int batchSize = 256,
        CancellationToken cancellationToken = default,
        DateTime? staleOperationCutoffUtc = null);

    /// <summary>Reconciles Reference query projections with canonical storage.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The reconciliation result.</returns>
    Task<ReferenceProjectionReconciliationResult> ReconcileQueryProjectionsV2Async(
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a lookup value.</summary>
    /// <param name="lookupTypeId">The lookup identity.</param>
    /// <returns>A task representing the command.</returns>
    Task DeleteLookupTypeAsync(LookupTypeId lookupTypeId);

    /// <summary>Deletes a scheduled job.</summary>
    /// <param name="jobId">The scheduled-job identifier.</param>
    /// <returns>A task representing the command.</returns>
    Task DeleteScheduledJobAsync(int jobId);

    /// <summary>Deletes MDI forward-loss ratios.</summary>
    /// <param name="trendDirection">The intrinsic-time trend direction.</param>
    /// <param name="tradeType">The trade type.</param>
    /// <returns>A task representing the command.</returns>
    Task DeleteMDIForwardLossRatioAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType);

    /// <summary>Inserts a lookup value.</summary>
    /// <param name="lookupType">The lookup value.</param>
    /// <returns>A task representing the command.</returns>
    Task InsertLookupTypeAsync(LookupTypeReadModel lookupType);

    /// <summary>Inserts a scheduled job.</summary>
    /// <param name="scheduledJob">The scheduled job.</param>
    /// <returns>A task representing the command.</returns>
    Task InsertScheduledJobAsync(ScheduledJobReadModel scheduledJob);

    /// <summary>Inserts one MDI forward-loss ratio.</summary>
    /// <param name="mdiForwardLossRatio">The ratio.</param>
    /// <returns>A task representing the command.</returns>
    Task InsertMDIForwardLossRatioAsync(MDIForwardLossRatioReadModel mdiForwardLossRatio);

    /// <summary>Inserts multiple MDI forward-loss ratios.</summary>
    /// <param name="mdiForwardLossRatios">The ratios.</param>
    /// <returns>A task representing the command.</returns>
    Task InsertMDIForwardLossRatiosAsync(ICollection<MDIForwardLossRatioReadModel> mdiForwardLossRatios);

    /// <summary>Updates a scheduled job.</summary>
    /// <param name="scheduledJob">The scheduled job.</param>
    /// <returns>A task representing the command.</returns>
    Task UpdateScheduledJobAsync(ScheduledJobReadModel scheduledJob);

    /// <summary>Updates a lookup value.</summary>
    /// <param name="id">The existing lookup identity.</param>
    /// <param name="lookupType">The replacement lookup value.</param>
    /// <returns>A task representing the command.</returns>
    Task UpdateLookupTypeAsync(LookupTypeId id, LookupTypeReadModel lookupType);

    /// <summary>Updates an MDI forward-loss ratio.</summary>
    /// <param name="mdiForwardLossRatio">The ratio.</param>
    /// <returns>A task representing the command.</returns>
    Task UpdateMDIForwardLossRatioAsync(MDIForwardLossRatioReadModel mdiForwardLossRatio);

    /// <summary>Inserts a trade-strategy-family definition.</summary>
    /// <param name="family">The family definition.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the command.</returns>
    Task InsertTradeStrategyFamilyAsync(TradeStrategyFamilyReadModel family, CancellationToken cancellationToken = default);
}
