using TomasAI.IFM.Shared.Application;
using TomasAI.IFM.Shared.Application.CommandParameters;
using TomasAI.IFM.Shared.Application.Commands;
using TomasAI.IFM.Shared.Application.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>
/// application command api constructor
/// </summary>
/// <param name="actorProducer"></param>
/// <exception cref="ArgumentNullException"></exception>
public class ApplicationCommandApi(IActorProducer actorProducer) 
    : NatsCommandApi(actorProducer), IApplicationCommandApi   
{

    /// start application
    /// </summary>
    /// <param name="cp"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> StartApplicationAsync(DateOnly valueDate)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new ApplicationEntityId(valueDate);
            StartApplicationCommand cmd = new(valueDate)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, StartApplicationCommand.Actor, StartApplicationCommand.Verb, entityId.Format()),
                EntityId = entityId,
                ErrorCode = StartApplicationCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, StartApplicationCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// shutdown application
    /// </summary>
    /// <param name="cp"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ShutdownApplicationAsync(DateOnly valueDate)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId  = new ApplicationEntityId(valueDate);
            ShutdownApplicationCommand cmd = new(valueDate)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, ShutdownApplicationCommand.Actor, ShutdownApplicationCommand.Verb, entityId.Format()),
                ErrorCode = ShutdownApplicationCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, ShutdownApplicationCommand.ErrorId);
        }
        return serviceResult;
    }
}

      