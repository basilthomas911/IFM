using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Services.Contracts;

internal interface ISpreadDistributionJobService
{
    ValueTask<ServiceResult<SpreadDistributionJobReadModel>> ExecuteAsync();
}
