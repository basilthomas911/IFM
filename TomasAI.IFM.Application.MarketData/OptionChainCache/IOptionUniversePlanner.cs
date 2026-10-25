using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using TomasAI.IFM.Shared.MarketData.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Application.MarketData.OptionChainCache;

/// <summary>Background-only normalized coverage planner; it performs no network or storage operations.</summary>
public interface IOptionUniversePlanner
{
    /// <summary>Plans the union of enabled biases from a complete definition scope using current volatility coverage.</summary>
    OptionUniversePlanResult Plan(StrategyOptionChainParameterSet parameters, DateOnly valueDate,
        ImmutableArray<OptionDefinitionCandidate> definitions, bool complete, decimal futuresPrice, double impliedVolatility,
        OptionPricingCalendar calendar, TreasuryPublicationPolicy publication, TreasuryRateConversionPolicy conversion, IReadOnlyDictionary<string, decimal>? underlyingPrices = null);
}
