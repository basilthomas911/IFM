using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;

namespace TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi
{
    public interface IOptionPricerServiceApi
    {
        Task<ServiceResult<SpreadDistributionJobReadModel>> ExecuteAsync(SpreadDistributionJobReadModel spreadDistributionJob);
    }
}
