using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventSourcing;

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
