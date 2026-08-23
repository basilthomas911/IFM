using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Reference.LookupType.Query.Actor;

/// <summary>Defines the runtime services required by <see cref="LookupTypeQueryActor"/>.</summary>
public interface ILookupTypeQueryContext : IQueryActorContext<LookupTypeQueryActor>
{
    /// <summary>Gets the database-context factory.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the query actor logger.</summary>
    ILogger<LookupTypeQueryActor> Logger { get; }
}
