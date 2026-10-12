using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>Identity of an already prepared scope.</summary>
public sealed record OptionChainCacheScope(string ScopeId, Guid GenerationId);
