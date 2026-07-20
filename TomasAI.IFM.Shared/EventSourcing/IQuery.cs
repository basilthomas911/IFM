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
public interface IQuery<TResult> : IQuery
{
}

/// <summary>
/// Represents a parameter used to define or modify the behavior of a query operation.
/// </summary>
/// <remarks>Implement this interface to provide custom query parameters for query-building APIs. Query parameters
/// typically specify filtering, sorting, paging, or other options that affect the results returned by a
/// query.</remarks>
public interface IQueryParameter
{
    public string? QueryParams { get; } 
}   
