using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.Commands;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>
/// create option pricer command api
/// </summary>
/// <param name="actorProducer  "></param>
/// <exception cref="ArgumentNullException"></exception>
public class OptionPricerCommandApi(IActorProducer actorProducer)
    : NatsCommandApi(actorProducer), IOptionPricerCommandApi
{

    /// <summary>
    /// insert spread distribution 
    /// </summary>
    /// <param name="putSpreadDistribution"></param>
    /// <param name="callSpreadDistribution"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> InsertSpreadDistributionsAsync(
        SpreadDistributionReadModel putSpreadDistribution,
        SpreadDistributionReadModel callSpreadDistribution)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            IsArgumentNull.Check(putSpreadDistribution);
            IsArgumentNull.Check(callSpreadDistribution);
            var entityId = new SpreadDistributionEntityId(putSpreadDistribution.TradeId,putSpreadDistribution.ValueDate);
            var cmd = new InsertSpreadDistributionCommand(putSpreadDistribution, callSpreadDistribution)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb, entityId.Format()),
                ErrorCode = InsertSpreadDistributionCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, InsertSpreadDistributionCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Deletes the specified spread distribution asynchronously.
    /// </summary>
    /// <remarks>Ensure that the provided entity ID corresponds to an existing spread distribution. The
    /// operation is performed asynchronously and may fail if the specified distribution does not exist.</remarks>
    /// <param name="e">The identifier of the spread distribution to delete. Must contain a valid trade ID and value date.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a ServiceResult with the unique
    /// identifier of the deleted spread distribution.</returns>
    public async Task<ServiceResult<Guid>> DeleteSpreadDistributionAsync(SpreadDistributionEntityId e, TradeStatus tradeStatus, int daysToExpiry)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            IsArgumentNull.Check(e);
            var entityId = e;
            var cmd = new DeleteSpreadDistributionCommand(entityId.TradeId, entityId.ValueDate, tradeStatus, daysToExpiry)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, DeleteSpreadDistributionCommand.Actor, DeleteSpreadDistributionCommand.Verb, entityId.Format()),
                ErrorCode = DeleteSpreadDistributionCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, DeleteSpreadDistributionCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// submit spread distribution job
    /// </summary>
    /// <param name="spreadDistributionJob"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> SubmitSpreadDistributionJobAsync(SpreadDistributionJobReadModel spreadDistributionJob)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            IsArgumentNull.Check(spreadDistributionJob);
            var cmd = new SubmitSpreadDistributionJobCommand(spreadDistributionJob)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, SubmitSpreadDistributionJobCommand.Actor, SubmitSpreadDistributionJobCommand.Verb, string.Empty),
                ErrorCode = SubmitSpreadDistributionJobCommand.ErrorId
            };
            var entityId = cmd.EntityId;
            cmd = cmd with { Subject = new ActorSubject(ActorType.Command, SubmitSpreadDistributionJobCommand.Actor, SubmitSpreadDistributionJobCommand.Verb, entityId.Format()) };
            serviceResult = await SendAsync(cmd, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, SubmitSpreadDistributionJobCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// complete spread distribution job
    /// </summary>
    /// <param name="entityId"></param>
    /// <param name="jobCompleted"></param>
    /// <param name="jobStatus"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> CompleteSpreadDistributionJobAsync(
        SpreadDistributionJobEntityId entityId,
        DateTime jobCompleted,
        SpreadDistributionJobStatus jobStatus)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            IsArgumentNull.Check(entityId);
            var cmd = new CompleteSpreadDistributionJobCommand(entityId, jobCompleted, jobStatus)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, CompleteSpreadDistributionJobCommand.Actor, CompleteSpreadDistributionJobCommand.Verb, entityId.Format()),
                ErrorCode = CompleteSpreadDistributionJobCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd, cmd.EntityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, CompleteSpreadDistributionJobCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// fail spread distribution job
    /// </summary>
    /// <param name="entityId"></param>
    /// <param name="jobFailed"></param>
    /// <param name="jobStatus"></param>
    /// <param name="errorMessage"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> FailSpreadDistributionJobAsync(
        SpreadDistributionJobEntityId entityId,
        DateTime jobFailed,
        SpreadDistributionJobStatus jobStatus,
        string errorMessage)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            IsArgumentNull.Check(entityId);
            var cmd = new FailSpreadDistributionJobCommand(entityId, jobFailed, jobStatus, errorMessage ?? string.Empty)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, FailSpreadDistributionJobCommand.Actor, FailSpreadDistributionJobCommand.Verb, entityId.Format()),
                ErrorCode = FailSpreadDistributionJobCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd, cmd.EntityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, FailSpreadDistributionJobCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// clear spread distribution jobs
    /// </summary>
    /// <param name="entityId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ClearSpreadDistributionJobAsync(SpreadDistributionJobEntityId entityId)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            IsArgumentNull.Check(entityId);
            var cmd = new ClearSpreadDistributionJobCommand(entityId)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, ClearSpreadDistributionJobCommand.Actor, ClearSpreadDistributionJobCommand.Verb, entityId.Format()),
                ErrorCode = ClearSpreadDistributionJobCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd, cmd.EntityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, ClearSpreadDistributionJobCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// delete spread distribution jobs in progress
    /// </summary>
    /// <param name="entityId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> DeleteSpreadDistributionJobsInProgressAsync(SpreadDistributionJobEntityId entityId)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            IsArgumentNull.Check(entityId);
            var cmd = new DeleteSpreadDistributionJobsInProgressCommand(entityId)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, DeleteSpreadDistributionJobsInProgressCommand.Actor, DeleteSpreadDistributionJobsInProgressCommand.Verb, entityId.Format()),
                ErrorCode = DeleteSpreadDistributionJobsInProgressCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd, cmd.EntityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, DeleteSpreadDistributionJobsInProgressCommand.ErrorId);
        }
        return serviceResult;
    }
}
