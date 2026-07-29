using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.OptionPricer.Shared.Events;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi
{
    public interface ISpreadDistributionJobApi
    {
        Task<ServiceResult> SpreadDistributionJobCreatedAsync(SpreadDistributionJobSubmittedEvent e);
        Task SpreadDistributionJobCompletedAsync(SpreadDistributionJobReadModel spreadDistributionJob);
        bool IsBusy();
    }
}
