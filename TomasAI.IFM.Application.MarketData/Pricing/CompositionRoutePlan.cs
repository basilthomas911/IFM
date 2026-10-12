using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Immutable reconstruction inputs. Contains no quote, rate observation, generation, lease or business authority.</summary>
public sealed record CompositionRoutePlan(int SchemaVersion, string PlanId, string Dataset,
    DateOnly MaturityDate, ImmutableArray<OptionDefinitionCandidate> Options,
    ImmutableArray<CompositionFutureDefinition> Futures, ImmutableArray<DatasetSubscriptionContract> NativeFutures,
    OptionPricingCalendar? Calendar, TreasuryPublicationPolicy? Publication, TreasuryRateConversionPolicy? Conversion)
{
    /// <summary>Requests a separate physical connection for the single option definition; false preserves existing plan hashes.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool SeparateContractConnection { get; init; }

    /// <summary>Retains an independent background chain connection across durable handoff and worker replacement.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool IndependentChainConnection { get; init; }

    public CompositionRoutePlan Seal() => this with { PlanId = PricingSemanticHash.Compute(this with { PlanId = "" }) };

    public void Validate()
    {
        if (SchemaVersion != 1 || !string.Equals(Dataset,"GLBX.MDP3",StringComparison.Ordinal) || !string.Equals(PlanId,Seal().PlanId,StringComparison.Ordinal)
            || Options.IsDefault || Futures.IsDefault || NativeFutures.IsDefaultOrEmpty
            || SeparateContractConnection && (Options.Length != 1 || IndependentChainConnection)
            || IndependentChainConnection && Options.IsEmpty
            || Options.Length > 2048 || Futures.Length > 16 || NativeFutures.Length > 16
            || Options.IsEmpty == Futures.IsEmpty
            || NativeFutures.Select(x => x.DomainContractId).Distinct(StringComparer.Ordinal).Count() != NativeFutures.Length
            || NativeFutures.Any(x => !string.Equals(x.Dataset,Dataset,StringComparison.Ordinal) || x.OnTheRun || x.Rollover)
            || !Options.IsEmpty && (Calendar is null || Publication is null || Conversion is null || MaturityDate == default)
            || Options.Any(x => !NativeFutures.Any(f => string.Equals(f.DomainContractId,x.Definition.Underlying,StringComparison.Ordinal)))
            || Futures.Any(x => !NativeFutures.Any(f => string.Equals(f.DomainContractId,x.ContractId,StringComparison.Ordinal))))
            throw new InvalidDataException("Invalid immutable composition reconstruction plan.");
        _ = new DatasetSubscriptionManifest(Dataset, new(2000, 1, 1), 1, NativeFutures);
    }

    public static ImmutableArray<DatasetSubscriptionContract> ResolveNative(DatasetDesiredSubscriptionRegistry registry,
        string dataset, DateOnly valueDate, IEnumerable<string> contractIds)
    {
        ArgumentNullException.ThrowIfNull(registry);
        if (!registry.TryGet(dataset, valueDate, out var manifest)) throw new CompositionMarketSourceException("Recovering");
        return contractIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(id =>
            (manifest.Contracts.SingleOrDefault(x => string.Equals(x.DomainContractId,id,StringComparison.Ordinal))
                ?? throw new CompositionMarketSourceException("UnderlyingRouteUnavailable")) with
            { OnTheRun = false, Rollover = false }).ToImmutableArray();
    }
}
