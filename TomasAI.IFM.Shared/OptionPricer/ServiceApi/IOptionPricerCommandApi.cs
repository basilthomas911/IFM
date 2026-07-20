using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.OptionPricer.ServiceApi;

public interface IOptionPricerCommandApi
{
    Task<ServiceResult<Guid>> InsertSpreadDistributionsAsync(SpreadDistributionReadModel putSpreadDistribution, SpreadDistributionReadModel callSpreadDistribution);
    Task<ServiceResult<Guid>> DeleteSpreadDistributionAsync(SpreadDistributionEntityId entityId, TradeStatus tradeStatus, int daysToExpiry);
    Task<ServiceResult<Guid>> SubmitSpreadDistributionJobAsync(SpreadDistributionJobReadModel spreadDistributionJob);
    Task<ServiceResult<Guid>> CompleteSpreadDistributionJobAsync(SpreadDistributionJobEntityId entityId, DateTime jobCompleted, SpreadDistributionJobStatus jobStatus);
    Task<ServiceResult<Guid>> FailSpreadDistributionJobAsync(SpreadDistributionJobEntityId entityId, DateTime jobFailed, SpreadDistributionJobStatus jobStatus, string errorMessage);
    Task<ServiceResult<Guid>> ClearSpreadDistributionJobAsync(SpreadDistributionJobEntityId entityId);
    Task<ServiceResult<Guid>> DeleteSpreadDistributionJobsInProgressAsync(SpreadDistributionJobEntityId entityId);
}
