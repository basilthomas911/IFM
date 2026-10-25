using System.Collections.Immutable;
using System.Text.Json.Serialization;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Shared.MarketData.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>
/// Reviewed bounded market universe embedded in the exact published composition policy. Option definitions
/// reference immutable ReferenceDb mappings; these are neither guessed from ES nor selected by workflow horizon.
/// </summary>
public sealed record CompositionMarketDataPlan
{
    [JsonRequired] public int SchemaVersion { get; init; } = 1;
    [JsonRequired] public string Dataset { get; init; } = "GLBX.MDP3";
    [JsonRequired] public string Root { get; init; } = "ES";
    [JsonRequired] public DateOnly ValueDate { get; init; }
    [JsonRequired] public bool IncludeOptions { get; init; }
    [JsonRequired] public bool ScopeComplete { get; init; }
    [JsonRequired] public DateOnly MaturityDate { get; init; }
    [JsonRequired] public ImmutableArray<OptionDefinitionCandidate> Options { get; init; } = [];
    [JsonRequired] public ImmutableArray<CompositionFutureDefinition> Futures { get; init; } = [];
    public OptionPricingCalendar? Calendar { get; init; }
    public TreasuryPublicationPolicy? Publication { get; init; }
    public TreasuryRateConversionPolicy? Conversion { get; init; }
}
