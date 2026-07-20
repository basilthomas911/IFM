using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Api.Nats.Client;

public class NatsCommandApi(IActorProducer actorProducer)
{
    readonly IActorProducer _actorProducer = IsArgumentNull.Set(actorProducer);

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TCommand"></typeparam>
    /// <typeparam name="TEntityId"></typeparam>
    /// <param name="command"></param>
    /// <param name="entityId"></param>
    /// <returns></returns>
    protected async ValueTask<ServiceResult<Guid>> SendAsync<TCommand, TEntityId>(TCommand command, TEntityId entityId)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
    { 
        await _actorProducer.SendAsync(command.Subject, command!, entityId);
        return  new ServiceResult<Guid>()
        {
            Success = true,
            Value = command.CommandId
        };
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TQuery"></typeparam>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="subject"></param>
    /// <param name="query"></param>
    /// <returns></returns>
    protected async ValueTask<ServiceResult<TResult>> RequestAsync<TQuery, TResult>(ActorSubject subject, TQuery query)
        where TQuery : class, IQuery<TResult>
        where TResult : class
    {
        return await _actorProducer.RequestAsync<TResult, TQuery>(subject, query);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ex"></param>
    /// <param name="cmdId"></param>
    /// <param name="errorCode"></param>
    /// <returns></returns>
    static protected ServiceResult<Guid> OnError(Exception ex, Guid cmdId, int errorCode )
        => new ()
        {
            Success = false,
            Value = cmdId,
            ErrorCode = errorCode,
            ErrorMessage = ex.Message
        };
}
