using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.OptionPricerDb;

public interface IOptionPricerDbWriteContext 
{
    Task DeleteOptionPricerDeviceAsync(OptionPricerDeviceEntityId e);
    Task DeleteSpreadDistributionAsync(int tradeId, DateOnly valueDate);
    Task DeleteSpreadDistributionJobsAsync(int orderId, int tradeId);
    Task DeleteSpreadDistributionJobsInProgressAsync();
    Task InsertOptionPricerDeviceAsync(OptionPricerDeviceReadModel device);
    Task InsertSpreadDistributionsAsync(SpreadDistributionReadModel putSpreadDistribution, SpreadDistributionReadModel callSpreadDistribution);
    Task InsertSpreadDistributionJobAsync(SpreadDistributionJobReadModel e);
    Task UpdateSpreadDistributionJobStatusAsync(int orderId, int tradeId, SpreadDistributionJobStatus jobStatus, DateTime jobCompleted, DateTime? jobFailed = null);
}
