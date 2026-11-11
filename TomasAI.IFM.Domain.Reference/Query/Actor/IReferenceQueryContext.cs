using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Reference.Query.Actor;

/// <summary>Defines the runtime services required by <see cref="ReferenceQueryActor"/>.</summary>
public interface IReferenceQueryContext : IQueryActorContext<ReferenceQueryActor>
{
    /// <summary>Gets the application owner that qualifies strategy catalog projections.</summary>
    TomasAI.IFM.Domain.Reference.StrategyCatalog.StrategyCatalogService? Catalog => null;
    TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi? MarketDataApi => null;
    /// <summary>Gets the database-context factory.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the query actor logger.</summary>
    ILogger<ReferenceQueryActor> Logger { get; }
}
