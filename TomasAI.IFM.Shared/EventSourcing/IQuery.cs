using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>
/// Represents a query with a subject, error code, and optional query parameters.
/// </summary>
/// <remarks>This interface defines the structure for a query, including the subject of the query,  an error code
/// indicating the query's status, and optional parameters for additional context.</remarks>
public interface IQuery
{
    ActorSubject Subject { get; }
    IActorEntityId EntityId { get; }
    int ErrorCode { get; }
    string? QueryParams { get; }
}

/// <summary>
/// Represents a query that returns a result of the specified type.
/// </summary>
/// <typeparam name="TResult">The type of the result produced by the query.</typeparam>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2326", Justification = "The invariant generic argument identifies the exact actor, state, result or handler contract in dependency registration and dispatch.")]
public interface IQuery<TResult> : IQuery
{
}
