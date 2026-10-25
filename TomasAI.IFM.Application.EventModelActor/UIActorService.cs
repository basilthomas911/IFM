using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.EventModelActor;

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
        => await SendAsync(command, entityId, CancellationToken.None).ConfigureAwait(false);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/channel boundary isolates arbitrary handler failures, records the failed outcome, and keeps processing other messages.")]
    public async ValueTask<ServiceResult<Guid>> SendAsync<TCommand, TEntityId>(
        TCommand command,
        TEntityId entityId,
        CancellationToken cancellationToken)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
    {
        ArgumentNullException.ThrowIfNull(command);
        try
        {
            IsArgumentNull.Check(command);
            var actorResult = await _producer.RequestAsync<TCommand, TEntityId, GuidResult>(
                command.Subject,
                command,
                entityId,
                cancellationToken).ConfigureAwait(false);
            return ActorCommandResult.ToGuidResult<TCommand, TEntityId>(command, actorResult);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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
        => await RequestAsync<TResult, TQuery>(query, CancellationToken.None).ConfigureAwait(false);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/channel boundary isolates arbitrary handler failures, records the failed outcome, and keeps processing other messages.")]
    public async ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(
        TQuery query,
        CancellationToken cancellationToken)
        where TQuery : class, IQuery<TResult>
        where TResult : class
    {
        try
        {
            IsArgumentNull.Check(query);
            return await _producer.RequestAsync<TResult, TQuery>(query.Subject, query, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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
        => await RequestAsync<TCommand, TEntityId>(command, CancellationToken.None).ConfigureAwait(false);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/channel boundary isolates arbitrary handler failures, records the failed outcome, and keeps processing other messages.")]
    public async ValueTask<ServiceResult<Guid>> RequestAsync<TCommand, TEntityId>(
        TCommand command,
        CancellationToken cancellationToken)
        where TEntityId : IActorEntityId
        where TCommand : class, ICommand<TEntityId>
    {
        try
        {
            IsArgumentNull.Check(command);
            var actorResult = await _producer.RequestAsync<TCommand, TEntityId, GuidResult>(
                command.Subject,
                command,
                command.EntityId,
                cancellationToken).ConfigureAwait(false);
            return ActorCommandResult.ToGuidResult<TCommand, TEntityId>(command, actorResult);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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

