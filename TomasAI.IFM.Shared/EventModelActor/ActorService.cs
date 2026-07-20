using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Provides a service for interacting with actors, enabling the execution of queries, commands, and events in an
/// actor-based system. This service acts as a mediator between the caller and the underlying actor infrastructure,
/// ensuring proper routing and execution of requests.
/// </summary>
/// <remarks>The <see cref="ActorService"/> is designed to work with an <see cref="IActorSupervisor"/> to manage
/// communication with actors. It supports sending commands and events, as well as requesting results from queries. The
/// service ensures that all requests are routed to the appropriate actor mailbox based on the subject of the request. 
/// This service is intended for use in systems that follow the actor model, where actors are isolated units of
/// computation and state, and communication is achieved through message passing.</remarks>
/// <param name="supervisor">The actor supervisor used to resolve producers and route messages to actor mailboxes.</param>
public class ActorService(IActorSupervisor supervisor)
    : IActorService
{
    readonly IActorSupervisor _supervisor = IsArgumentNull.Set(supervisor);

    /// <summary>
    /// Sends the specified command asynchronously to the appropriate mailbox.
    /// </summary>
    /// <remarks>This method resolves the appropriate producer for the command's subject and sends the command
    /// to the corresponding mailbox. Ensure that the <paramref name="command"/> is properly initialized and contains a
    /// valid subject and entity identifier.</remarks>
    /// <typeparam name="TCommand">The type of the command to send. Must implement <see cref="ICommand{TEntityId}"/>.</typeparam>
    /// <typeparam name="TEntityId">The type of the entity identifier associated with the command.</typeparam>
    /// <param name="command">The command to be sent. Cannot be <see langword="null"/>.</param>
    /// <param name="entityId">The entity identifier that the command targets.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation. The result contains
    /// a <see cref="ServiceResult{T}"/> with the command identifier.</returns>
    public async ValueTask<ServiceResult<Guid>> SendAsync<TCommand, TEntityId>(TCommand command, TEntityId entityId)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
    {
        try
        {
            IsArgumentNull.Check(command);
            var producer = _supervisor.GetProducer(command.Subject.ActorId);
            await producer.SendAsync(command.Subject, command, entityId);
            return new ServiceResult<Guid>(command.CommandId);
        }
        catch (Exception ex)
        {
            return new ServiceResult<Guid>
            {
                Success = false,
                ErrorMessage = ex.Message,
                Value = command.CommandId,
                ErrorCode = command.ErrorCode
            };
        }
    }
    /// <summary>
    /// Sends a query to the appropriate producer and asynchronously retrieves the result.
    /// </summary>
    /// <typeparam name="TResult">The type of the result expected from the query. Must be a reference type.</typeparam>
    /// <typeparam name="TQuery">The type of the query to send. Must implement <see cref="IQuery{TResult}"/>.</typeparam>
    /// <param name="query">The query to be sent. Cannot be <see langword="null"/>.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation. The result contains
    /// a <see cref="ServiceResult{T}"/> with the query response.</returns>
    public async ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(TQuery query)
        where TQuery : class, IQuery<TResult>
        where TResult : class
    {
        try 
        {
            IsArgumentNull.Check(query);
            var producer = _supervisor.GetProducer(query.Subject.ActorId);
            return await producer.RequestAsync<TResult, TQuery>(query.Subject, query);
        }
        catch (Exception ex)
        {
            // Handle exceptions as needed
            return new ServiceResult<TResult>
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Sends a command to the appropriate actor and awaits a response.
    /// </summary>
    /// <typeparam name="TCommand">The type of the command to send. Must implement <see cref="ICommand{TEntityId}"/>.</typeparam>
    /// <typeparam name="TEntityId">The type of the entity identifier, which must implement <see cref="IActorEntityId"/>.</typeparam>
    /// <param name="command">The command to be sent to the actor. Cannot be <see langword="null"/>.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation. The result contains a <see
    /// cref="ServiceResult{T}"/> with the unique identifier of the processed command.</returns>
    public async ValueTask<ServiceResult<Guid>> RequestAsync<TCommand,TEntityId>(TCommand command) 
        where TEntityId : IActorEntityId
        where TCommand : class, ICommand<TEntityId>
    {
        try
        {
            IsArgumentNull.Check(command);
            var producer = _supervisor.GetProducer(command.Subject.ActorId);
            var actorResult =  await producer.RequestAsync<TCommand, TEntityId, GuidResult>(command.Subject, command, command.EntityId);
            return new ServiceResult<Guid>
            {
                Success = actorResult.Success,
                ErrorCode = command.ErrorCode,
                ErrorMessage = actorResult.ErrorMessage,
                ErrorEvent = actorResult.ErrorEvent,
                Value = command.CommandId,
            };
        }
        catch (Exception ex)
        {
            // Handle exceptions as needed
            return new ServiceResult<Guid>
            {
                Success = false,
                ErrorCode = command.ErrorCode,
                ErrorMessage = ex.Message
            };
        }
    }
 }

/// <summary>
/// Provides an actor service implementation for UI clients that communicates through a single
/// <see cref="IActorProducer"/> rather than resolving producers via a supervisor.
/// </summary>
/// <remarks>This implementation is intended for scenarios where the caller already holds a reference to
/// the target producer, such as UI-layer event consumers that interact with the actor system over a
/// pre-established connection.</remarks>
/// <param name="producer">The actor producer used to send commands and queries to the actor system.</param>
public class UIActorService(IActorProducer producer)
    : IActorService
{
    readonly IActorProducer _producer = IsArgumentNull.Set(producer);

    /// <summary>
    /// Sends the specified command asynchronously to the actor system via the configured producer.
    /// </summary>
    /// <typeparam name="TCommand">The type of the command to send. Must implement <see cref="ICommand{TEntityId}"/>.</typeparam>
    /// <typeparam name="TEntityId">The type of the entity identifier associated with the command.</typeparam>
    /// <param name="command">The command to be sent. Cannot be <see langword="null"/>.</param>
    /// <param name="entityId">The entity identifier that the command targets.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation. The result contains
    /// a <see cref="ServiceResult{T}"/> with the command identifier.</returns>
    public async ValueTask<ServiceResult<Guid>> SendAsync<TCommand, TEntityId>(TCommand command, TEntityId entityId)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
    {
        try
        {
            IsArgumentNull.Check(command);
            await _producer.SendAsync(command.Subject, command, entityId);
            return new ServiceResult<Guid>(command.CommandId);
        }
        catch (Exception ex)
        {
            return new ServiceResult<Guid>
            {
                Success = false,
                ErrorMessage = ex.Message,
                Value = command.CommandId,
                ErrorCode = command.ErrorCode
            };
        }
    }
    /// <summary>
    /// Sends a query to the configured producer and asynchronously retrieves the result.
    /// </summary>
    /// <typeparam name="TResult">The type of the result expected from the query. Must be a reference type.</typeparam>
    /// <typeparam name="TQuery">The type of the query to send. Must implement <see cref="IQuery{TResult}"/>.</typeparam>
    /// <param name="query">The query to be sent. Cannot be <see langword="null"/>.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation. The result contains
    /// a <see cref="ServiceResult{T}"/> with the query response.</returns>
    public async ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(TQuery query)
        where TQuery : class, IQuery<TResult>
        where TResult : class
    {
        try
        {
            IsArgumentNull.Check(query);
            return await _producer.RequestAsync<TResult, TQuery>(query.Subject, query);
        }
        catch (Exception ex)
        {
            // Handle exceptions as needed
            return new ServiceResult<TResult>
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Sends a command to the actor system via the configured producer and awaits a response.
    /// </summary>
    /// <typeparam name="TCommand">The type of the command to send. Must implement <see cref="ICommand{TEntityId}"/>.</typeparam>
    /// <typeparam name="TEntityId">The type of the entity identifier, which must implement <see cref="IActorEntityId"/>.</typeparam>
    /// <param name="command">The command to be sent to the actor. Cannot be <see langword="null"/>.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation. The result contains a <see
    /// cref="ServiceResult{T}"/> with the unique identifier of the processed command.</returns>
    public async ValueTask<ServiceResult<Guid>> RequestAsync<TCommand, TEntityId>(TCommand command)
        where TEntityId : IActorEntityId
        where TCommand : class, ICommand<TEntityId>
    {
        try
        {
            IsArgumentNull.Check(command);
            var actorResult = await _producer.RequestAsync<TCommand, TEntityId, GuidResult>(command.Subject, command, command.EntityId);
            return new ServiceResult<Guid>(command.CommandId);
        }
        catch (Exception ex)
        {
            // Handle exceptions as needed
            return new ServiceResult<Guid>
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}

