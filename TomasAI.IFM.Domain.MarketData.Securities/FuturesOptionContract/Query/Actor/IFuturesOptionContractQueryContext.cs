using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Query.Actor;

/// <summary>Defines the runtime services required by <see cref="FuturesOptionContractQueryActor"/>.</summary>
public interface IFuturesOptionContractQueryContext : IQueryActorContext<FuturesOptionContractQueryActor>
{
    /// <summary>Gets the database-context factory.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesOptionContractQueryActor> Logger { get; }
}
