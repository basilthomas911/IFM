using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;

public interface IReferenceDbWriteContext 
{
    Task<ReferenceProjectionBackfillResult> BackfillQueryProjectionsV2Async(
        int batchSize = 256,
        CancellationToken cancellationToken = default,
        DateTime? staleOperationCutoffUtc = null)
        => throw new NotSupportedException();
    Task<ReferenceProjectionReconciliationResult> ReconcileQueryProjectionsV2Async(
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
    Task DeleteLookupTypeAsync(LookupTypeId lookupTypeId);
    Task DeleteScheduledJobAsync(int jobId);
    Task DeleteMDIForwardLossRatioAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType);
    Task InsertLookupTypeAsync(LookupTypeReadModel lookupType);
    Task InsertScheduledJobAsync(ScheduledJobReadModel scheduledJob);
    Task InsertMDIForwardLossRatioAsync(MDIForwardLossRatioReadModel mdiForwardLossRatio);
    Task InsertMDIForwardLossRatiosAsync(ICollection<MDIForwardLossRatioReadModel> mdiForwardLossRatios);
    Task UpdateScheduledJobAsync(ScheduledJobReadModel scheduledJob);
    Task UpdateLookupTypeAsync(LookupTypeId id, LookupTypeReadModel e);
    Task UpdateMDIForwardLossRatioAsync(MDIForwardLossRatioReadModel mdiForwardLossRatio);
    Task InsertTradeStrategyFamilyAsync(TradeStrategyFamilyReadModel family, CancellationToken cancellationToken = default);
}
