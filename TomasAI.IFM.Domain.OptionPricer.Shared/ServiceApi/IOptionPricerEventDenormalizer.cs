using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using TomasAI.IFM.Domain.OptionPricer.Shared.Events;

namespace TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi
{
    public interface IOptionPricerEventDenormalizerApi
    {
        Task InsertSpreadDistributionAsync(SpreadDistributionInsertedEvent e);
        Task InsertSpreadDistributionJobAsync(SpreadDistributionJobSubmittedEvent e);
        Task UpdateSpreadDistributionJobCompletedAsync(SpreadDistributionJobStatusUpdatedEvent e);
    }
}
