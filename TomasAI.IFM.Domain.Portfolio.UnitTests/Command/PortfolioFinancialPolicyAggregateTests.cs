using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using System.Text.Json;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Command;

public sealed class PortfolioFinancialPolicyAggregateTests
{
    static readonly DateTime Now = new(2026, 8, 30, 16, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Gate", "PF-23")]
    public void Contracts_round_trip_with_exact_integer_identity_and_deterministic_hash()
    {
        var policy = ValidPolicy();

        var copy = MessagePackSerializer.Deserialize<PortfolioFinancialPolicyReadModel>(MessagePackSerializer.Serialize(policy));

        copy.Should().BeEquivalentTo(policy);
        copy.PolicyId.Should().Be(9001);
        copy.CanonicalSha256().Should().Be(policy.CanonicalSha256());
        var reordered = copy with { TradeFamilyLimits = [.. copy.TradeFamilyLimits.Reverse()] };
        reordered.CanonicalSha256().Should().Be(policy.CanonicalSha256());
    }

    [Fact]
    [Trait("Gate", "PF-23")]
    [Trait("Gate", "PF-27")]
    public void Validation_rejects_family_cap_above_global_and_duplicate_versioned_family()
    {
        var policy = ValidPolicy() with
        {
            TradeFamilyLimits =
            [
                Family(1) with { MaximumMargin = 600_000m },
                Family(1),
            ]
        };

        policy.Validate(forActivation: true).Should().Contain(x => x.Contains("exceeds", StringComparison.OrdinalIgnoreCase));
        policy.Validate(forActivation: true).Should().Contain(x => x.Contains("unique", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(true, 1_000, 5_000, 20_000, 200_000, 5, true)]
    [InlineData(false, 1_000, 5_000, 20_000, 200_000, 5, false)]
    [InlineData(true, 0, 5_000, 20_000, 200_000, 5, false)]
    [Trait("Gate", "PF-23")]
    [Trait("Gate", "PF-27")]
    [Trait("Gate", "PF-29")]
    public void Effective_caps_are_most_restrictive_and_zero_or_disabled_fails_closed(
        bool enabled, decimal familyRisk, decimal familyAggregate, decimal familyMargin, decimal familyNotional,
        int familyPositions, bool permits)
    {
        var policy = ValidPolicy() with
        {
            TradeFamilyLimits = [Family(1) with
            {
                Enabled = enabled, MaximumRiskPerTrade = familyRisk, MaximumAggregateRisk = familyAggregate,
                MaximumMargin = familyMargin, MaximumGrossNotional = familyNotional, MaximumOpenPositions = familyPositions
            }]
        };
        var envelope = Envelope() with
        {
            MaximumRiskPerTrade = 750, MaximumAggregateRisk = 6_000, MaximumMargin = 15_000,
            MaximumGrossNotional = 250_000, MaximumOpenPositions = 4
        };

        var effective = policy.ResolveEffectiveCaps(1, 1, envelope, Now);

        effective.MaximumRiskPerTrade.Should().Be(Math.Min(familyRisk, 750));
        effective.MaximumAggregateRisk.Should().Be(Math.Min(familyAggregate, 6_000));
        effective.MaximumMargin.Should().Be(Math.Min(familyMargin, 15_000));
        effective.MaximumGrossNotional.Should().Be(Math.Min(familyNotional, 250_000));
        effective.MaximumOpenPositions.Should().Be(Math.Min(familyPositions, 4));
        effective.PermitsNewExposure.Should().Be(permits);
    }

    [Fact]
    [Trait("Gate", "PF-24")]
    [Trait("Gate", "PF-27")]
    public void Immutable_version_activation_supersedes_prior_version_and_replays_identically()
    {
        var aggregate = new PortfolioFinancialPolicyAggregate();
        List<PortfolioFinancialPolicyDomainEvent> history = [];
        history.Add(aggregate.Create(Guid.NewGuid(), Guid.NewGuid(), ValidPolicy(), Now, "risk-admin"));
        history.Add(aggregate.Activate(Guid.NewGuid(), 1, 1, Now.AddMinutes(1), "risk-admin"));
        history.Add(aggregate.AddVersion(Guid.NewGuid(), 2, ValidPolicy() with
        {
            PolicyVersion = 2, Name = "Core limits v2", OperatingState = PortfolioFinancialPolicyState.Draft,
            CreatedOnUtc = Now.AddMinutes(2)
        }, Now.AddMinutes(2), "risk-admin"));
        history.Add(aggregate.Activate(Guid.NewGuid(), 3, 2, Now.AddMinutes(3), "risk-admin"));
        var replay = new PortfolioFinancialPolicyAggregate();

        replay.Replay(history);

        replay.Revision.Should().Be(4);
        replay.Current!.PolicyVersion.Should().Be(2);
        replay.Current.OperatingState.Should().Be(PortfolioFinancialPolicyState.Active);
        replay.Versions.Single(x => x.PolicyVersion == 1).OperatingState.Should().Be(PortfolioFinancialPolicyState.Superseded);
        replay.Versions.Should().BeEquivalentTo(aggregate.Versions);
    }

    [Fact]
    [Trait("Gate", "PF-24")]
    [Trait("Gate", "PF-27")]
    public void Delete_is_allowed_only_for_never_active_unreferenced_draft()
    {
        var draft = new PortfolioFinancialPolicyAggregate();
        draft.Create(Guid.NewGuid(), Guid.NewGuid(), ValidPolicy(), Now, "risk-admin");
        draft.DeleteDraft(Guid.NewGuid(), 1, "duplicate", false, Now.AddMinutes(1), "risk-admin");

        draft.IsDeleted.Should().BeTrue();

        var active = new PortfolioFinancialPolicyAggregate();
        active.Create(Guid.NewGuid(), Guid.NewGuid(), ValidPolicy(), Now, "risk-admin");
        active.Activate(Guid.NewGuid(), 1, 1, Now.AddMinutes(1), "risk-admin");
        var action = () => active.DeleteDraft(Guid.NewGuid(), 2, "invalid", false, Now.AddMinutes(2), "risk-admin");
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Catalog_risk_limits_are_exact_versioned_and_legacy_zero_cannot_match_them()
    {
        var key = new CatalogKey(StrategyCatalogKind.Deployment, Guid.NewGuid(), 1);
        var second = key with { Version = 2 };
        var policy = ValidPolicy() with { SchemaVersion = 3, TradeFamilyLimits = [Family(0) with { DefinitionVersion = 0, CatalogDeployment = key, MaximumRiskPerTrade = 500 }, Family(0) with { DefinitionVersion = 0, CatalogDeployment = second, MaximumRiskPerTrade = 1500 }] };
        policy.Validate().Should().BeEmpty();
        policy.ResolveEffectiveCaps(key, Envelope(), Now).MaximumRiskPerTrade.Should().Be(500);
        policy.ResolveEffectiveCaps(second, Envelope(), Now).MaximumRiskPerTrade.Should().Be(1500);
        policy.ResolveEffectiveCaps(key, Envelope(), Now).CatalogDeployment.Should().Be(key);
        var legacyLookup = () => policy.ResolveEffectiveCaps(0, 0, Envelope(), Now); legacyLookup.Should().Throw<InvalidOperationException>();
        (policy with { TradeFamilyLimits = policy.TradeFamilyLimits.Reverse().ToArray() }).CanonicalSha256().Should().Be(policy.CanonicalSha256());
        JsonSerializer.Serialize(ValidPolicy()).Should().NotContain("CatalogDeployment");
        (ValidPolicy() with { SchemaVersion = 3 }).Validate().Should().Contain(x => x.Contains("ConfigurationDb"));
    }

    internal static PortfolioFinancialPolicyReadModel ValidPolicy() => new()
    {
        PortfolioId = 101, PolicyId = 9001, PolicyVersion = 1, Name = "Core limits",
        OperatingState = PortfolioFinancialPolicyState.Draft, BaseCurrency = "USD", CapitalBase = 1_000_000m,
        ProtectedReserve = 100_000m, MaximumDeployableCapital = 900_000m,
        MaximumRiskPerTrade = 10_000m, MaximumAggregateRisk = 100_000m, MaximumMargin = 500_000m,
        MaximumGrossNotional = 5_000_000m, MaximumOpenPositions = 100, MaximumDrawdownAmount = 200_000m,
        TradeFamilyLimits = [Family(1), Family(2), Family(3)],
        EffectiveFromUtc = Now.AddMinutes(-1), CreatedOnUtc = Now, CreatedBy = "risk-admin"
    };

    static TradeFamilyRiskLimitReadModel Family(int id) => new()
    {
        TradeStrategyFamilyId = id, DefinitionVersion = 1, Enabled = true,
        MaximumRiskPerTrade = 5_000m, MaximumAggregateRisk = 50_000m, MaximumMargin = 250_000m,
        MaximumGrossNotional = 2_500_000m, MaximumOpenPositions = 50
    };

    static FundRiskEnvelopeReadModel Envelope() => new()
    {
        PortfolioId = 101, PortfolioVersion = 1, FundId = 201, FundMandateVersion = 1,
        EnvelopeId = Guid.NewGuid(), EnvelopeVersion = 1, CapacityState = FundCapacityState.Available,
        AllocatedCapital = 100_000m, AvailableCapital = 90_000m, MaximumRiskPerTrade = 2_000m,
        MaximumAggregateRisk = 10_000m, MaximumMargin = 50_000m, MaximumGrossNotional = 500_000m,
        MaximumContracts = 10, MaximumOpenPositions = 10, RemainingLossBudget = 20_000m,
        EffectiveFromUtc = Now.AddMinutes(-1), ExpiresAtUtc = Now.AddHours(1),
        SourcePolicyId = 9001, SourcePolicyVersion = 1, CreatedOnUtc = Now, CreatedBy = "risk-admin"
    };
}
