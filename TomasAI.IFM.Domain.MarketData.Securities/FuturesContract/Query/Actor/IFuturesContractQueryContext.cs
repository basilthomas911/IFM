using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Query.Actor;

/// <summary>Defines the runtime services required by <see cref="FuturesContractQueryActor"/>.</summary>
public interface IFuturesContractQueryContext : IQueryActorContext<FuturesContractQueryActor>
{
    /// <summary>Gets the database-context factory.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesContractQueryActor> Logger { get; }
}
