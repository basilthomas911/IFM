using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;

/// <summary>
/// Defines NATS-backed Option Pricer commands intended for use by domain event actors.
/// </summary>
public interface IActorOptionPricerCommandApi
{
    ValueTask<ServiceResult<GuidResult>> SubmitSpreadDistributionJobAsync(
        SpreadDistributionJobReadModel spreadDistributionJob);

    ValueTask<ServiceResult<GuidResult>> CompleteSpreadDistributionJobAsync(
        SpreadDistributionJobEntityId entityId,
        DateTime jobCompleted,
        SpreadDistributionJobStatus jobStatus);

    ValueTask<ServiceResult<GuidResult>> FailSpreadDistributionJobAsync(
        SpreadDistributionJobEntityId entityId,
        DateTime jobFailed,
        SpreadDistributionJobStatus jobStatus,
        string errorMessage);
}

public interface IActorOptionPricerCommandApiFactory
{
    IActorOptionPricerCommandApi Create(IEventActorContext context);
}
