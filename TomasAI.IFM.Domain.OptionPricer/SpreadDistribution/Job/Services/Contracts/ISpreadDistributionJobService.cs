using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Services.Contracts;

internal interface ISpreadDistributionJobService
{
    ValueTask<ServiceResult<SpreadDistributionJobReadModel>> ExecuteAsync();
}
