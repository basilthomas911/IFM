using TomasAI.IFM.Domain.Reference.Shared.Commands;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Api.Nats.Client;

public class ReferenceCommandApi(IActorProducer actorProducer)
    : NatsClientApi(actorProducer), IReferenceCommandApi
{
    public Task<ServiceResult<Guid>> ChangeTradeStrategyFamilyAsync(ChangeTradeStrategyFamilyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request); cancellationToken.ThrowIfCancellationRequested();
        var command = new ChangeTradeStrategyFamilyCommand { CommandId = request.OperationId, Request = request,
            Subject = new ActorSubject(ActorType.Command, ChangeTradeStrategyFamilyCommand.Actor, ChangeTradeStrategyFamilyCommand.Verb, ActorEntityId.Default.Format()) };
        return RequestCommandAsync(command, command.EntityId, cancellationToken).AsTask();
    }
    public Task<ServiceResult<Guid>> RemoveTradeStrategyFamilyAsync(RemoveTradeStrategyFamilyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request); cancellationToken.ThrowIfCancellationRequested();
        var command = new RemoveTradeStrategyFamilyCommand { CommandId = request.OperationId, Request = request,
            Subject = new ActorSubject(ActorType.Command, RemoveTradeStrategyFamilyCommand.Actor, RemoveTradeStrategyFamilyCommand.Verb, ActorEntityId.Default.Format()) };
        return RequestCommandAsync(command, command.EntityId, cancellationToken).AsTask();
    }
    public Task<ServiceResult<Guid>> CreateTradeStrategyFamilyAsync(CreateTradeStrategyFamilyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var command = new CreateTradeStrategyFamilyCommand
        {
            CommandId = request.OperationId, Request = request,
            Subject = new ActorSubject(ActorType.Command, CreateTradeStrategyFamilyCommand.Actor, CreateTradeStrategyFamilyCommand.Verb, ActorEntityId.Default.Format())
        };
        return RequestCommandAsync(command, command.EntityId, cancellationToken).AsTask();
    }
    /// <summary>
    /// add lookup type
    /// </summary>
    /// <param name="lookupType"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> AddLookupTypeAsync(LookupTypeReadModel lookupType)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            IsArgumentNull.Check(lookupType);
            var entityId = lookupType.Id;
            var cmd = new AddLookupTypeCommand(lookupType)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, AddLookupTypeCommand.Actor, AddLookupTypeCommand.Verb, entityId.Format()),
                ErrorCode = AddLookupTypeCommand.ErrorId
            };
            serviceResult = await RequestCommandAsync(cmd, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, AddLookupTypeCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// change lookup type
    /// </summary>
    /// <param name="lookupTypeId"></param>
    /// <param name="lookupType"></param>
    /// <param name="overwrite"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ChangeLookupTypeAsync(LookupTypeId lookupTypeId, LookupTypeReadModel lookupType, bool overwrite)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            IsArgumentNull.Check(lookupTypeId);
            IsArgumentNull.Check(lookupType);
            var cmd = new ChangeLookupTypeCommand(lookupTypeId, lookupType, overwrite)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, ChangeLookupTypeCommand.Actor, ChangeLookupTypeCommand.Verb, lookupTypeId.Format()),
                ErrorCode = ChangeLookupTypeCommand.ErrorId
            };
            serviceResult = await RequestCommandAsync(cmd, cmd.EntityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, ChangeLookupTypeCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// remove lookup type
    /// </summary>
    /// <param name="lookupTypeId"></param>
    /// <param name="overwrite"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> RemoveLookupTypeAsync(LookupTypeId lookupTypeId, bool overwrite)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            IsArgumentNull.Check(lookupTypeId);
            var cmd = new RemoveLookupTypeCommand(lookupTypeId, overwrite)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, RemoveLookupTypeCommand.Actor, RemoveLookupTypeCommand.Verb, lookupTypeId.Format()),
                ErrorCode = RemoveLookupTypeCommand.ErrorId
            };
            serviceResult = await RequestCommandAsync(cmd, cmd.EntityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, RemoveLookupTypeCommand.ErrorId);
        }
        return serviceResult;
    }
}
