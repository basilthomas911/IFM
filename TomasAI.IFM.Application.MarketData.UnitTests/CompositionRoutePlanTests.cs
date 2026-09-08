using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;
using static TomasAI.IFM.Application.MarketData.UnitTests.OrderCompositionPricingPrerequisiteTests;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class CompositionRoutePlanTests
{
    [Fact]
    public void Option_reconstruction_roundtrips_without_observations_or_generation_and_detects_tampering()
    {
        var c = Contract();
        var candidate = new OptionDefinitionCandidate(c.ContractId, c.MappingVersion, c.DefinitionDigest, new()
        {
            Dataset = c.Dataset, RawSymbol = c.RawSymbol, Ticker = c.Root, Underlying = c.UnderlyingContractId,
            Instrument = new(c.PublisherId, c.InstrumentId), Right = OptionRightSelection.Call,
            StrikePrice = 5000, MaturityDate = new(2026, 10, 2),
            ExpirationTimestampNanoseconds = checked((ulong)(c.ExpirationUtc.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100)
        });
        var plan = new CompositionRoutePlan(1, "", "GLBX.MDP3", new(2026, 10, 2), [candidate], [],
            [Route(c.UnderlyingContractId, "ESZ6")], Calendar(), Publication(), Conversion).Seal();
        plan.Validate();
        var saved = JsonSerializer.Deserialize<CompositionRoutePlan>(JsonSerializer.Serialize(plan))!;
        saved.Validate();
        Assert.Equal(plan.PlanId, saved.PlanId);
        Assert.Equal(c.InstrumentId, saved.Options[0].Definition.Instrument.InstrumentId);
        Assert.Throws<InvalidDataException>(() => (saved with { MaturityDate = new(2026, 10, 3) }).Validate());
    }

    [Fact]
    public void Committed_futures_survive_core_rollover_until_explicit_overlay_removal()
    {
        var registry = new DatasetDesiredSubscriptionRegistry();
        var date = new DateOnly(2026, 9, 8);
        var old = Route("ES-old", "ESU6"); var next = Route("ES-next", "ESZ6");
        registry.Set("GLBX.MDP3", date, [(old with { OnTheRun = true }).ToRegistration()]);
        registry.SetDurable("GLBX.MDP3", date, [old]);
        var rolled = registry.Set("GLBX.MDP3", date.AddDays(1), [(next with { OnTheRun = true }).ToRegistration()]);
        Assert.Equal(2, rolled.Contracts.Count);
        Assert.False(rolled.Contracts.Single(x => x.DomainContractId == old.DomainContractId).OnTheRun);
        Assert.Throws<InvalidOperationException>(() => registry.SetDurable("GLBX.MDP3", date, []));
        var removed = registry.SetDurable("GLBX.MDP3", date.AddDays(1), []);
        Assert.Equal(next.DomainContractId, Assert.Single(removed.Contracts).DomainContractId);
        Assert.Equal(removed.Revision, registry.SetDurable("GLBX.MDP3", date.AddDays(1), []).Revision);
    }

    [Fact]
    public void Conflicting_durable_mapping_does_not_mutate_existing_native_manifest()
    {
        var registry = new DatasetDesiredSubscriptionRegistry(); var date = new DateOnly(2026, 9, 8);
        var old = Route("ES-old", "ESU6");
        var current = registry.Set("GLBX.MDP3", date, [(old with { OnTheRun = true }).ToRegistration()]);
        Assert.Throws<InvalidDataException>(() => registry.SetDurable("GLBX.MDP3", date, [old with { ProviderContractName = "ESZ6" }]));
        Assert.True(registry.IsCurrent(current));
    }

    static DatasetSubscriptionContract Route(string id, string symbol) => new()
    {
        Dataset = "GLBX.MDP3", DomainContractId = id, ProviderContractName = symbol,
        AssetTypeId = AssetTypeId.Futures, RootSymbol = "ES"
    };
}
