using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>Resident publication status; Ready still requires request-specific quality validation.</summary>
public sealed record OptionChainCacheStatus(bool Published, long ChainVersion, DateTimeOffset? PublishedAtUtc,
    DateTimeOffset? ValidUntilUtc, int ContractCount, string ReasonCode);
