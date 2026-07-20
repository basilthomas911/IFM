using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Defines an interface for sending queries and commands to actor-based services and receiving typed results
/// asynchronously.
/// </summary>
/// <remarks>Implementations of this interface provide a mechanism for interacting with actor entities using
/// strongly typed requests. The methods return a ServiceResult that encapsulates the outcome of the operation,
/// including success or failure information. This interface is intended for use in distributed or concurrent systems
/// where actor patterns are employed.</remarks>
public interface IActorService
{
    ValueTask<ServiceResult<Guid>> SendAsync<TCommand, TEntityId>(TCommand command, TEntityId entityId)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId;

    ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(TQuery query)
        where TQuery : class, IQuery<TResult>
        where TResult : class;

    ValueTask<ServiceResult<Guid>> RequestAsync<TCommand, TEntityId>(TCommand command)
        where TCommand: class, ICommand<TEntityId>
        where TEntityId : IActorEntityId;
}
