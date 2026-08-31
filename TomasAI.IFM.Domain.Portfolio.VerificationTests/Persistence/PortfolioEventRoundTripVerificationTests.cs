using FluentAssertions;
using Newtonsoft.Json;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.VerificationTests.Persistence;

public sealed class PortfolioEventRoundTripVerificationTests
{
    [Fact]
    [Trait("Gate", "PF-07")]
    public void Every_defined_Portfolio_event_type_survives_event_store_serialization()
    {
        var now = new DateTime(2026, 8, 29, 17, 0, 0, DateTimeKind.Utc);
        var portfolio = new PortfolioReadModel { PortfolioId = 101, Name = "Core", PortfolioVersion = 1, OperatingState = PortfolioOperatingState.Draft, EffectiveFromUtc = now, CreatedOnUtc = now, CreatedBy = "verify" };
        var mandate = new FundMandateReadModel { PortfolioId = 101, FundId = 205, FundCode = "DIR", Name = "Directional", FundMandateVersion = 1, TradingYear = 2026, OperatingState = FundOperatingState.Draft, DecisionHorizon = "Daily", Objective = "Directional futures", UnderlyingUniverse = ["ES"], EligibleAssetTypes = ["Futures"], PermittedTradeFamilies = ["Futures"], EffectiveFromUtc = now, CreatedOnUtc = now, CreatedBy = "verify" };
        var assignment = new FundTradeTemplateAssignmentReadModel { PortfolioId = 101, PortfolioVersion = 1, FundId = 205, FundMandateVersion = 1, AssignmentVersion = 1, TradeTemplateId = Guid.NewGuid(), TradeTemplateVersion = 1, Enabled = true, DecisionHorizon = "Daily", UnderlyingUniverse = ["ES"], AssetType = "Futures", TradeFamily = "Futures", Priority = 1, EffectiveFromUtc = now, TradeSelectionHintProfileId = Guid.NewGuid(), TradeSelectionHintProfileVersion = 1, OrderCompositionProfileId = Guid.NewGuid(), OrderCompositionProfileVersion = 1, CreatedOnUtc = now, CreatedBy = "verify" };
        PortfolioDomainEvent[] portfolioEvents =
        [
            new PortfolioCreated(Guid.NewGuid(), Guid.NewGuid(), 1, now, "verify", portfolio),
            new PortfolioVersionAdded(Guid.NewGuid(), Guid.NewGuid(), 2, now, "verify", portfolio with { PortfolioVersion = 2 }),
            new PortfolioOperatingStateChanged(Guid.NewGuid(), Guid.NewGuid(), 3, now, "verify", PortfolioOperatingState.Disabled, "pause"),
            new FundAddedToPortfolio(Guid.NewGuid(), Guid.NewGuid(), 4, now, "verify", new PortfolioFundId(101, 205)),
            new PortfolioRetired(Guid.NewGuid(), Guid.NewGuid(), 5, now, "verify", "closed"),
            new DraftPortfolioDeleted(Guid.NewGuid(), Guid.NewGuid(), 6, now, "verify", "duplicate draft")
        ];
        PortfolioFundDomainEvent[] fundEvents =
        [
            new FundMandateCreated(Guid.NewGuid(), Guid.NewGuid(), 1, now, "verify", mandate),
            new FundMandateVersionAdded(Guid.NewGuid(), Guid.NewGuid(), 2, now, "verify", mandate with { FundMandateVersion = 2 }),
            new FundOperatingStateChanged(Guid.NewGuid(), Guid.NewGuid(), 3, now, "verify", FundOperatingState.Disabled, "pause"),
            new FundTradeTemplateAssigned(Guid.NewGuid(), Guid.NewGuid(), 4, now, "verify", assignment)
        ];
        var policy = new PortfolioFinancialPolicyReadModel
        {
            PortfolioId = 101, PolicyId = 9001, PolicyVersion = 1, Name = "Limits", OperatingState = PortfolioFinancialPolicyState.Draft,
            CapitalBase = 1_000_000, MaximumDeployableCapital = 900_000, MaximumRiskPerTrade = 10_000,
            MaximumAggregateRisk = 100_000, MaximumMargin = 500_000, MaximumGrossNotional = 5_000_000,
            MaximumOpenPositions = 100, MaximumDrawdownAmount = 200_000,
            TradeFamilyLimits = [new() { TradeStrategyFamilyId = 1, DefinitionVersion = 1, Enabled = true, MaximumRiskPerTrade = 5_000, MaximumAggregateRisk = 50_000, MaximumMargin = 250_000, MaximumGrossNotional = 2_500_000, MaximumOpenPositions = 50 }],
            EffectiveFromUtc = now, CreatedOnUtc = now, CreatedBy = "verify"
        };
        PortfolioFinancialPolicyDomainEvent[] policyEvents =
        [
            new PortfolioFinancialPolicyCreated(Guid.NewGuid(), Guid.NewGuid(), 1, now, "verify", policy, Guid.NewGuid()),
            new PortfolioFinancialPolicyVersionAdded(Guid.NewGuid(), Guid.NewGuid(), 2, now, "verify", policy with { PolicyVersion = 2 }),
            new PortfolioFinancialPolicyActivated(Guid.NewGuid(), Guid.NewGuid(), 3, now, "verify", 2),
            new PortfolioFinancialPolicyRetired(Guid.NewGuid(), Guid.NewGuid(), 4, now, "verify", 1, "superseded"),
        ];

        portfolioEvents.Cast<object>().Concat(fundEvents).Concat(policyEvents).Select(RoundTrip).Should().OnlyContain(x => x);
    }

    static bool RoundTrip(object source)
    {
        var type = source.GetType();
        var row = new EventStreamReadModel { EventTypeName = type.AssemblyQualifiedName!, EventData = JsonConvert.SerializeObject(source), EventVersion = 1, StreamVersion = 1 };
        return row.ToDomainEvent().GetType() == type;
    }

    [Fact]
    [Trait("Gate", "PF-07")]
    public void Event_only_and_snapshot_accelerated_rebuilds_have_identical_immutable_view_hashes()
    {
        var now = new DateTime(2026, 8, 29, 19, 0, 0, DateTimeKind.Utc);
        var source = new PortfolioAggregate();
        var first = source.Create(Guid.NewGuid(), new PortfolioReadModel
        {
            PortfolioId = 303, Name = "Hash", PortfolioVersion = 1,
            OperatingState = PortfolioOperatingState.Draft, EffectiveFromUtc = now, CreatedOnUtc = now, CreatedBy = "verify"
        }, now, "verify");
        var snapshot = source.CaptureSnapshot();
        var second = source.ChangeState(Guid.NewGuid(), 1, PortfolioOperatingState.Disabled, "pause", now.AddSeconds(1), "verify");
        var eventOnly = new PortfolioAggregate();
        eventOnly.Replay([first, second]);
        var accelerated = new PortfolioAggregate();
        accelerated.RestoreSnapshot(snapshot);
        accelerated.Replay([second]);

        Hash(eventOnly.CaptureSnapshot()).Should().Equal(Hash(accelerated.CaptureSnapshot()));
    }

    static byte[] Hash(object value) => System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value, Formatting.None)));
}
