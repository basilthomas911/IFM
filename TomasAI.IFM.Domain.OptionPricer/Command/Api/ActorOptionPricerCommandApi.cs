using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.Commands;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.OptionPricer.Command.Api;

public sealed class ActorOptionPricerCommandApi(IEventActorContext context)
    : IActorOptionPricerCommandApi
{
    readonly IEventActorContext _context = IsArgumentNull.Set(context);

    public ValueTask<ServiceResult<GuidResult>> SubmitSpreadDistributionJobAsync(
        SpreadDistributionJobReadModel spreadDistributionJob)
    {
        var entityId = spreadDistributionJob.EntityId;
        SubmitSpreadDistributionJobCommand command = new(spreadDistributionJob)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                SubmitSpreadDistributionJobCommand.Actor,
                SubmitSpreadDistributionJobCommand.Verb,
                entityId.Format()),
            EntityId = entityId,
            ErrorCode = SubmitSpreadDistributionJobCommand.ErrorId
        };
        return RequestAsync<SubmitSpreadDistributionJobCommand, SpreadDistributionJobEntityId>(command);
    }

    public ValueTask<ServiceResult<GuidResult>> CompleteSpreadDistributionJobAsync(
        SpreadDistributionJobEntityId entityId,
        DateTime jobCompleted,
        SpreadDistributionJobStatus jobStatus)
    {
        CompleteSpreadDistributionJobCommand command = new(entityId, jobCompleted, jobStatus)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                CompleteSpreadDistributionJobCommand.Actor,
                CompleteSpreadDistributionJobCommand.Verb,
                entityId.Format()),
            EntityId = entityId,
            ErrorCode = CompleteSpreadDistributionJobCommand.ErrorId
        };
        return RequestAsync<CompleteSpreadDistributionJobCommand, SpreadDistributionJobEntityId>(command);
    }

    public ValueTask<ServiceResult<GuidResult>> FailSpreadDistributionJobAsync(
        SpreadDistributionJobEntityId entityId,
        DateTime jobFailed,
        SpreadDistributionJobStatus jobStatus,
        string errorMessage)
    {
        FailSpreadDistributionJobCommand command = new(entityId, jobFailed, jobStatus, errorMessage)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                FailSpreadDistributionJobCommand.Actor,
                FailSpreadDistributionJobCommand.Verb,
                entityId.Format()),
            EntityId = entityId,
            ErrorCode = FailSpreadDistributionJobCommand.ErrorId
        };
        return RequestAsync<FailSpreadDistributionJobCommand, SpreadDistributionJobEntityId>(command);
    }

    async ValueTask<ServiceResult<GuidResult>> RequestAsync<TCommand, TEntityId>(TCommand command)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
    {
        var result = await _context.RequestAsync<TCommand, TEntityId>(command);
        if (result?.Success != true)
            throw new InvalidOperationException(result?.ErrorMessage);
        return result;
    }
}

public sealed class ActorOptionPricerCommandApiFactory : IActorOptionPricerCommandApiFactory
{
    public IActorOptionPricerCommandApi Create(IEventActorContext context)
        => new ActorOptionPricerCommandApi(context);
}
