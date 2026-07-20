using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;

namespace TomasAI.IFM.Application.Storage.OptionPricerDb
{
    public interface IOptionPricerDbWriteContext : IOptionPricerDbContext
    {
        Task InsertOptionPricerDeviceAsync(OptionPricerDeviceReadModel device);
        Task InsertSpreadDistributionsAsync(SpreadDistributionReadModel putSpreadDistribution, SpreadDistributionReadModel callSpreadDistribution);
        Task InsertSpreadDistributionJobAsync(SpreadDistributionJobReadModel e);
        Task UpdateSpreadDistributionJobStatusAsync(int jobId, DateTime jobCompleted, SpreadDistributionJobStatus jobStatus);
        Task DeleteSpreadDistributionJobsInProgressAsync();
        Task DeleteSpreadDistributionJobsAsync(int orderId, int trdaeId);

    }
}
