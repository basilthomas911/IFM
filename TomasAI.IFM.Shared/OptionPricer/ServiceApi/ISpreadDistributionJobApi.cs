using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.OptionPricer.Events;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.OptionPricer.ServiceApi
{
    public interface ISpreadDistributionJobApi
    {
        Task<ServiceResult> SpreadDistributionJobCreatedAsync(SpreadDistributionJobSubmittedEvent e);
        Task SpreadDistributionJobCompletedAsync(SpreadDistributionJobReadModel spreadDistributionJob);
        bool IsBusy();
    }
}
