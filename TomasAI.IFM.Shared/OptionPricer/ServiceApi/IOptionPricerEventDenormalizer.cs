using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using TomasAI.IFM.Shared.OptionPricer.Events;

namespace TomasAI.IFM.Shared.OptionPricer.ServiceApi
{
    public interface IOptionPricerEventDenormalizerApi
    {
        Task InsertSpreadDistributionAsync(SpreadDistributionInsertedEvent e);
        Task InsertSpreadDistributionJobAsync(SpreadDistributionJobSubmittedEvent e);
        Task UpdateSpreadDistributionJobCompletedAsync(SpreadDistributionJobStatusUpdatedEvent e);
    }
}
