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
    public CompositionRoutePlan Seal() => this with { PlanId = PricingSemanticHash.Compute(this with { PlanId = "" }) };

    public void Validate()
    {
        if (SchemaVersion != 1 || Dataset != "GLBX.MDP3" || PlanId != Seal().PlanId
            || Options.IsDefault || Futures.IsDefault || NativeFutures.IsDefaultOrEmpty
            || Options.Length > 512 || Futures.Length > 16 || NativeFutures.Length > 16
            || Options.IsEmpty == Futures.IsEmpty
            || NativeFutures.Select(x => x.DomainContractId).Distinct(StringComparer.Ordinal).Count() != NativeFutures.Length
            || NativeFutures.Any(x => x.Dataset != Dataset || x.OnTheRun || x.Rollover)
            || !Options.IsEmpty && (Calendar is null || Publication is null || Conversion is null || MaturityDate == default)
            || Options.Any(x => !NativeFutures.Any(f => f.DomainContractId == x.Definition.Underlying))
            || Futures.Any(x => !NativeFutures.Any(f => f.DomainContractId == x.ContractId)))
            throw new InvalidDataException("Invalid immutable composition reconstruction plan.");
        _ = new DatasetSubscriptionManifest(Dataset, new(2000, 1, 1), 1, NativeFutures);
    }

    public static ImmutableArray<DatasetSubscriptionContract> ResolveNative(DatasetDesiredSubscriptionRegistry registry,
        string dataset, DateOnly valueDate, IEnumerable<string> contractIds)
    {
        if (!registry.TryGet(dataset, valueDate, out var manifest)) throw new CompositionMarketSourceException("Recovering");
        return contractIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(id =>
            (manifest.Contracts.SingleOrDefault(x => x.DomainContractId == id)
                ?? throw new CompositionMarketSourceException("UnderlyingRouteUnavailable")) with { OnTheRun = false, Rollover = false }).ToImmutableArray();
    }
}

public interface ICompositionRoutePlanStore
{
    Task SaveAsync(CompositionRoutePlan plan, CancellationToken cancellationToken);
    Task<CompositionRoutePlan?> ReadAsync(string planId, CancellationToken cancellationToken);
}
