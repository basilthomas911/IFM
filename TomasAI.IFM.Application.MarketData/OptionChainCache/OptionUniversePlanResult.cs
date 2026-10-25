using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using TomasAI.IFM.Shared.MarketData.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Application.MarketData.OptionChainCache;

/// <summary>Result of bounded background coverage planning. Capacity failures never truncate an apparently complete scope.</summary>
public sealed record OptionUniversePlanResult(ImmutableArray<PreparedOptionUniverse> Universes, string ReasonCode);
