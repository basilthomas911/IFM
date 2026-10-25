using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>
/// Defines a parameter that can be supplied to a command for execution or configuration.
/// </summary>
/// <remarks>Implementations of this interface represent values or settings that influence command behavior. The
/// specific contract and usage depend on the command system in which this interface is used.</remarks>
public interface ICommandParameter
{
    int ErrorCode { get; }
}

/// <summary>
/// Defines a parameter that can be supplied to a command for execution or configuration, associated with a specific entity identifier.
/// </summary>
/// <typeparam name="TEntityId"></typeparam>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3246", Justification = "The invariant generic argument identifies the exact actor, state, result or handler contract in dependency registration and dispatch.")]
public interface ICommandParameter<TEntityId>
    : ICommandParameter where TEntityId : IActorEntityId
{
    TEntityId EntityId { get; }
}

