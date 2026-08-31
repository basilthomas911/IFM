using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.VerificationTests.Command;

public sealed class PortfolioFinancialPolicyCatalogVerificationTests
{
    static readonly DateTime Now = new(2026, 8, 30, 17, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(1, 800, true)]
    [InlineData(2, 600, true)]
    [InlineData(3, 0, false)]
    [Trait("Gate", "PF-23")]
    [Trait("Gate", "PF-26")]
    [Trait("Gate", "PF-29")]
    [Trait("Category", "Portfolio")]
    public void Futures_vertical_spread_and_iron_condor_resolve_exact_most_restrictive_caps(int familyId, decimal expectedRisk, bool permits)
    {
        var policy = Policy();
        var envelope = new FundRiskEnvelopeReadModel
        {
            PortfolioId = 501, PortfolioVersion = 2, FundId = 601, FundMandateVersion = 3,
            EnvelopeId = Guid.NewGuid(), EnvelopeVersion = 1, CapacityState = FundCapacityState.Available,
            MaximumRiskPerTrade = 800, MaximumAggregateRisk = 9_000, MaximumMargin = 30_000,
            MaximumGrossNotional = 300_000, MaximumOpenPositions = 7, EffectiveFromUtc = Now.AddHours(-1),
            ExpiresAtUtc = Now.AddHours(1), SourcePolicyId = 9001, SourcePolicyVersion = 4,
            CreatedOnUtc = Now.AddHours(-1), CreatedBy = "verification"
        };

        var caps = policy.ResolveEffectiveCaps(familyId, 1, envelope, Now);

        caps.MaximumRiskPerTrade.Should().Be(expectedRisk);
        caps.PermitsNewExposure.Should().Be(permits);
        caps.MaximumMargin.Should().BeLessThanOrEqualTo(30_000);
        caps.MaximumGrossNotional.Should().BeLessThanOrEqualTo(300_000);
        caps.MaximumOpenPositions.Should().BeLessThanOrEqualTo(7);
    }

    static PortfolioFinancialPolicyReadModel Policy() => new()
    {
        PortfolioId = 501, PolicyId = 9001, PolicyVersion = 4, Name = "Representative limits",
        OperatingState = PortfolioFinancialPolicyState.Active, CapitalBase = 1_000_000,
        MaximumDeployableCapital = 900_000, MaximumRiskPerTrade = 10_000,
        MaximumAggregateRisk = 100_000, MaximumMargin = 500_000, MaximumGrossNotional = 5_000_000,
        MaximumOpenPositions = 100, MaximumDrawdownAmount = 200_000,
        TradeFamilyLimits =
        [
            Family(1, 2_000, 10_000, 50_000, 500_000, 10),
            Family(2, 600, 8_000, 25_000, 250_000, 6),
            Family(3, 0, 5_000, 20_000, 200_000, 5),
        ],
        EffectiveFromUtc = Now.AddDays(-1), CreatedOnUtc = Now.AddDays(-1), CreatedBy = "verification"
    };

    static TradeFamilyRiskLimitReadModel Family(int id, decimal risk, decimal aggregate, decimal margin, decimal notional, int positions) => new()
    {
        TradeStrategyFamilyId = id, DefinitionVersion = 1, Enabled = true,
        MaximumRiskPerTrade = risk, MaximumAggregateRisk = aggregate, MaximumMargin = margin,
        MaximumGrossNotional = notional, MaximumOpenPositions = positions
    };
}
