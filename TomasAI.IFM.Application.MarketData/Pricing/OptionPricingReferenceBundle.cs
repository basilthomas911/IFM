using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Published complete expiry definitions and the reviewed policies needed to construct bounded route plans.</summary>
public sealed record OptionPricingReferenceBundle(int SchemaVersion, string BundleId, string ProfileVersion, DateOnly MaturityDate,
    DatasetSubscriptionContract Underlying, OptionPricingCalendar Calendar, TreasuryPublicationPolicy Publication,
    TreasuryRateConversionPolicy Conversion, ImmutableArray<OptionDefinitionCandidate> Definitions)
{
    public OptionPricingReferenceBundle Seal() => this with { BundleId = PricingSemanticHash.Compute(this with { BundleId = "" }) };
    public void Validate()
    {
        if (SchemaVersion != 1 || BundleId != Seal().BundleId || string.IsNullOrWhiteSpace(ProfileVersion)
            || Definitions.IsDefaultOrEmpty || Definitions.Length > 10000 || Underlying.AssetTypeId != Domain.MarketData.Feed.Shared.TickAggregation.AssetTypeId.Futures
            || Underlying.OnTheRun || Underlying.Rollover || Definitions.Select(x => x.ContractId).Distinct().Count() != Definitions.Length
            || Definitions.Any(x => x.MappingVersion != ProfileVersion || x.Definition.MaturityDate != MaturityDate
                || x.Definition.Underlying != Underlying.DomainContractId || x.Definition.Dataset != Underlying.Dataset)
            || Calendar.CoverageFrom > MaturityDate || Calendar.CoverageUntil < MaturityDate)
            throw new InvalidDataException("Invalid complete option reference bundle.");
    }
    /// <summary>Explicit bounded strike scope, enumerated completely; no implicit first-page truncation.</summary>
    public CompositionRoutePlan CreatePlan(decimal fromStrike, decimal toStrike)
    {
        Validate();
        if (fromStrike <= 0 || toStrike < fromStrike) throw new ArgumentOutOfRangeException(nameof(fromStrike));
        var selected = Definitions.Where(x => x.Definition.StrikePrice >= fromStrike && x.Definition.StrikePrice <= toStrike)
            .OrderBy(x => x.ContractId, StringComparer.Ordinal).ToImmutableArray();
        var plan = new CompositionRoutePlan(1, "", Underlying.Dataset, MaturityDate, selected, [], [Underlying], Calendar, Publication, Conversion).Seal();
        plan.Validate(); return plan;
    }
}
