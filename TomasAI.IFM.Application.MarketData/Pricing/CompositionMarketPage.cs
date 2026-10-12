using System.Collections.Immutable;
using MessagePack;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>One atomic source view; scope token and generation must remain stable across all pages.</summary>
public sealed record CompositionMarketPage(string ScopeToken, Guid GenerationId, int TotalContracts,
    ImmutableArray<CompositionMarketInstrument> Instruments, string? Continuation);
