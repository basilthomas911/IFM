using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.BDDTests.Command;

public sealed class PortfolioFinancialPolicyScenarios
{
    static readonly DateTime Now = new(2026, 8, 30, 17, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Gate", "PF-23")]
    [Trait("Gate", "PF-24")]
    [Trait("Gate", "PF-27")]
    [Trait("Category", "Portfolio")]
    public void Given_three_broad_families_when_a_replacement_is_activated_then_the_old_version_is_superseded_and_the_new_version_is_active()
    {
        var policy = new PortfolioFinancialPolicyAggregate();
        policy.Create(Guid.NewGuid(), Guid.NewGuid(), Draft(1), Now, "risk-admin");
        policy.Activate(Guid.NewGuid(), 1, 1, Now.AddMinutes(1), "risk-admin");
        policy.AddVersion(Guid.NewGuid(), 2, Draft(2), Now.AddMinutes(2), "risk-admin");

        policy.Activate(Guid.NewGuid(), 3, 2, Now.AddMinutes(3), "risk-admin");

        policy.Current!.PolicyVersion.Should().Be(2);
        policy.Current.OperatingState.Should().Be(PortfolioFinancialPolicyState.Active);
        policy.Versions.Single(x => x.PolicyVersion == 1).OperatingState.Should().Be(PortfolioFinancialPolicyState.Superseded);
        policy.Versions.SelectMany(x => x.TradeFamilyLimits).Select(x => x.TradeStrategyFamilyId).Distinct().Should().Equal(1, 2, 3);
    }

    [Fact]
    [Trait("Gate", "PF-23")]
    [Trait("Gate", "PF-27")]
    [Trait("Category", "Portfolio")]
    public void Given_a_configured_but_zero_capacity_family_when_resolving_limits_then_new_exposure_is_blocked()
    {
        var policy = Draft(1) with
        {
            OperatingState = PortfolioFinancialPolicyState.Active,
            TradeFamilyLimits = [Family(1), Family(2), Family(3) with { MaximumRiskPerTrade = 0 }]
        };
        var envelope = new FundRiskEnvelopeReadModel
        {
            PortfolioId = 101, PortfolioVersion = 2, FundId = 201, FundMandateVersion = 1,
            EnvelopeId = Guid.NewGuid(), EnvelopeVersion = 1, CapacityState = FundCapacityState.Available,
            MaximumRiskPerTrade = 1_000, MaximumAggregateRisk = 5_000, MaximumMargin = 20_000,
            MaximumGrossNotional = 200_000, MaximumOpenPositions = 5, EffectiveFromUtc = Now.AddMinutes(-1),
            ExpiresAtUtc = Now.AddHours(1), SourcePolicyId = 9001, SourcePolicyVersion = 1,
            CreatedOnUtc = Now, CreatedBy = "risk-admin"
        };

        policy.ResolveEffectiveCaps(3, 1, envelope, Now).PermitsNewExposure.Should().BeFalse();
    }

    static PortfolioFinancialPolicyReadModel Draft(long version) => new()
    {
        PortfolioId = 101, PolicyId = 9001, PolicyVersion = version, Name = $"Core limits v{version}",
        OperatingState = PortfolioFinancialPolicyState.Draft, CapitalBase = 1_000_000,
        MaximumDeployableCapital = 900_000, MaximumRiskPerTrade = 10_000,
        MaximumAggregateRisk = 100_000, MaximumMargin = 500_000, MaximumGrossNotional = 5_000_000,
        MaximumOpenPositions = 100, MaximumDrawdownAmount = 200_000,
        TradeFamilyLimits = [Family(1), Family(2), Family(3)], EffectiveFromUtc = Now.AddMinutes(-1),
        CreatedOnUtc = Now, CreatedBy = "risk-admin"
    };

    static TradeFamilyRiskLimitReadModel Family(int id) => new()
    {
        TradeStrategyFamilyId = id, DefinitionVersion = 1, Enabled = true,
        MaximumRiskPerTrade = 5_000, MaximumAggregateRisk = 50_000, MaximumMargin = 250_000,
        MaximumGrossNotional = 2_500_000, MaximumOpenPositions = 50
    };
}
