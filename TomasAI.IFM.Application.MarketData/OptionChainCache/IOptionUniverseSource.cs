using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Application.MarketData.OptionChainCache;

/// <summary>Loads published strategy universes from persisted projections on the background path only.</summary>
public interface IOptionUniverseSource
{
    /// <summary>Returns the bounded published union for the admitted operational value date.</summary>
    Task<ImmutableArray<PreparedOptionUniverse>> LoadAsync(DateOnly valueDate, CancellationToken cancellationToken);
}
