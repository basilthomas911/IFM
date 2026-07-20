using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>
/// Defines the contract for a command that can be executed within an actor-based system, providing essential metadata
/// such as the command name, routing information, and identifiers.
/// </summary>
/// <remarks>Implementations of this interface represent discrete actions or requests that are processed by
/// actors. The properties expose information necessary for command handling, routing, and error reporting. Commands are
/// typically used to encapsulate intent and facilitate communication between bounded contexts or components.</remarks>
public interface ICommand
{
    ActorSubject Subject { get; init; }
    string CommandName { get; }
    BoundedContextName RouteTo { get; }
    Guid CommandId { get; init; }
    string StreamId { get; }
    string EventSource { get; }
    int ErrorCode { get; }
}

/// <summary>
/// Defines a parameter that can be supplied to a command for execution or configuration.
/// </summary>
/// <remarks>Implementations of this interface represent values or settings that influence command behavior. The
/// specific contract and usage depend on the command system in which this interface is used.</remarks>
public interface  ICommandParameter
{
    int ErrorCode { get; }
}

/// <summary>
/// Defines a parameter that can be supplied to a command for execution or configuration, associated with a specific entity identifier.
/// </summary>
/// <typeparam name="TEntityId"></typeparam>
public interface ICommandParameter<TEntityId>
    : ICommandParameter where TEntityId : IActorEntityId
{
    TEntityId EntityId { get; }
}

/// <summary>
/// Defines a command that targets a specific actor entity identified by its unique identifier.
/// </summary>
/// <typeparam name="TEntityId">The type of the actor entity identifier. Must implement <see cref="IActorEntityId"/>.</typeparam>
public interface ICommand<TEntityId>
    : ICommand  where TEntityId : IActorEntityId
{
    TEntityId EntityId { get; }
}

