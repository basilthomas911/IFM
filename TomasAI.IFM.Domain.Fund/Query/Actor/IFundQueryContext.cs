using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Fund.Query.Actor;

/// <summary>
/// Defines the runtime services required by <see cref="FundQueryActor"/> in addition to shared query actor operations.
/// </summary>
public interface IFundQueryContext : IQueryActorContext<FundQueryActor>
{
    /// <summary>
    /// Gets the database-context factory used by Fund queries.
    /// </summary>
    IDbContextFactory DbFactory { get; }

    /// <summary>
    /// Gets the logger associated with the Fund query actor.
    /// </summary>
    ILogger<FundQueryActor> Logger { get; }
}
