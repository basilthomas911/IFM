using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.Commands;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.OptionPricer.Command.Api;

/// <summary>
/// Sends Option Pricer job commands from a running event actor and returns their typed replies.
/// </summary>
/// <remarks>
/// The API creates submit, complete, and fail commands with a spread-distribution job identity before using
/// the captured <see cref="IEventActorContext"/> for request/reply messaging. Create instances through
/// <see cref="ActorOptionPricerCommandApiFactory"/> and do not share them between actors.
/// </remarks>
public sealed class ActorOptionPricerCommandApi(IEventActorContext context)
    : IActorOptionPricerCommandApi
{
    readonly IEventActorContext _context = IsArgumentNull.Set(context);

    /// <summary>
    /// Sends the submit spread distribution job command and awaits its typed actor reply.
    /// </summary>
    /// <param name="spreadDistributionJob">The spread-distribution job to submit.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
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

    /// <summary>
    /// Sends the complete spread distribution job command and awaits its typed actor reply.
    /// </summary>
    /// <param name="entityId">The target actor entity identifier.</param>
    /// <param name="jobCompleted">The job completion timestamp.</param>
    /// <param name="jobStatus">The resulting job status.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
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

    /// <summary>
    /// Sends the fail spread distribution job command and awaits its typed actor reply.
    /// </summary>
    /// <param name="entityId">The target actor entity identifier.</param>
    /// <param name="jobFailed">The job failure timestamp.</param>
    /// <param name="jobStatus">The resulting job status.</param>
    /// <param name="errorMessage">The failure description.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
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

/// <summary>
/// Creates Option Pricer command APIs bound to a running event actor.
/// </summary>
public sealed class ActorOptionPricerCommandApiFactory : IActorOptionPricerCommandApiFactory
{
    /// <summary>
    /// Creates a command API that dispatches through the supplied actor context.
    /// </summary>
    /// <param name="context">The actor context used for command request/reply messaging.</param>
    /// <returns>A context-bound Option Pricer command API.</returns>
    public IActorOptionPricerCommandApi Create(IEventActorContext context)
        => new ActorOptionPricerCommandApi(context);
}
