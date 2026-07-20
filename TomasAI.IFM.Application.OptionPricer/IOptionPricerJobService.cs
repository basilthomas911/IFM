using TomasAI.IFM.Shared.OptionPricer.Events;

namespace TomasAI.IFM.Application.OptionPricer
{
    public interface IOptionPricerJobService
    {
        Task RunOptionPricerJobAsync(SpreadDistributionJobSubmittedEvent e);
    }
}