using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Application.MarketData.OptionChainCache;

/// <summary>Published global scope, resolved outside workflow handling. Identity pins the complete reviewed universe.</summary>
public sealed record PreparedOptionUniverse(string ScopeId, string ConfigurationDigest,
    CompositionMarketDataPlan MarketData, ImmutableArray<string> Horizons)
{
    /// <summary>Exact published global policy; null is reserved for explicit reviewed legacy universes.</summary>
    public StrategyOptionChainParameterSet? Parameters { get; init; }
}
