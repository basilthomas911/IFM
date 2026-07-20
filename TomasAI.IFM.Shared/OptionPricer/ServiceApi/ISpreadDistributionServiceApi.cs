using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;

namespace TomasAI.IFM.Shared.OptionPricer.ServiceApi
{
    public interface ISpreadDistributionServiceApi
    {
        Task<ServiceResult<SpreadDistributionJobReadModel>> ExecuteAsync(SpreadDistributionJobReadModel spreadDistributionJob);
        Task CreateOptionSpreadPricerAsync();
        Task DestroyOptionSpreadPriceAsync();
    }
}
