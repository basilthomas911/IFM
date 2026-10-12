using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Api.Nats.Client;

public class NatsClientApi(IActorProducer actorProducer)
{
    readonly IActorProducer _actorProducer = IsArgumentNull.Set(actorProducer);

    /// <summary>Sends a standard typed Function request without changing its financial operation identity.</summary>
    protected ValueTask<ServiceResult<TResult>> RequestFunctionAsync<TCommand, TEntityId, TResult>(TCommand command, TEntityId entityId, CancellationToken token)
        where TCommand : class, ICommand<TEntityId> where TEntityId : IActorEntityId where TResult : class
        
    {
        ArgumentNullException.ThrowIfNull(command);
        return _actorProducer.RequestFunctionAsync<TCommand, TEntityId, TResult>(command.Subject, command, entityId, token);
    }

    /// <summary>
    /// Sends a command to an actor and waits for its domain result.
    /// </summary>
    protected async ValueTask<ServiceResult<Guid>> RequestCommandAsync<TCommand, TEntityId>(
        TCommand command,
        TEntityId entityId,
        CancellationToken cancellationToken = default)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
    {
        ArgumentNullException.ThrowIfNull(command);
        var actorResult = await (_actorProducer
            .RequestAsync<TCommand, TEntityId, GuidResult>(
                command.Subject,
                command,
                entityId,
                cancellationToken)).ConfigureAwait(false);
        return new ServiceResult<Guid>
        {
            Success = actorResult.Success,
            ErrorCode = actorResult.ErrorCode != 0 ? actorResult.ErrorCode : DefaultErrorCode(actorResult.Success, command.ErrorCode),
            ErrorMessage = actorResult.ErrorMessage,
            ErrorEvent = actorResult.ErrorEvent,
            Value = command.CommandId
        };
    }

    static int DefaultErrorCode(bool success, int commandErrorCode) => success ? 0 : commandErrorCode;

    protected async ValueTask<ServiceResult<TResult>> RequestCommandResultAsync<TCommand, TEntityId, TResult>(
        TCommand command,
        TEntityId entityId,
        CancellationToken cancellationToken = default)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(command);
        return await (_actorProducer.RequestAsync<TCommand, TEntityId, TResult>(command.Subject, command, entityId, cancellationToken)).ConfigureAwait(false);
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
        return await (_actorProducer.RequestAsync<TResult, TQuery>(subject, query)).ConfigureAwait(false);
    }

    protected async ValueTask<ServiceResult<TResult>> RequestAsync<TQuery, TResult>(ActorSubject subject, TQuery query, CancellationToken cancellationToken)
        where TQuery : class, IQuery<TResult>
        where TResult : class
    {
        return await (_actorProducer.RequestAsync<TResult, TQuery>(subject, query, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ex"></param>
    /// <param name="cmdId"></param>
    /// <param name="errorCode"></param>
    /// <returns></returns>
    static protected ServiceResult<Guid> OnError(Exception ex, Guid cmdId, int errorCode)
        
    {
        ArgumentNullException.ThrowIfNull(ex);
        return new()
        {
            Success = false,
            Value = cmdId,
            ErrorCode = errorCode,
            ErrorMessage = ex.Message
        };
    }
}
