using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Risk;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class StrategyRiskParameterSetTests
{
    [Fact]
    public void Development_default_has_explicit_daily_loss_and_two_direction_scenario_settings()
    {
        var set = StrategyRiskParameterSet.CreateIronCondorDevelopmentDefault();
        set.Validate();
        set.Environment.Should().Be("Development");
        set.InstrumentRoot.Should().Be("ES");
        set.IronCondor!.DailyLossLimit.Should().Be(1000);
        set.IronCondor.ScenarioHorizonSeconds.Should().Be(300);
        set.IronCondor.AdverseFuturesMovePoints.Should().Be(10);
        set.IronCondor.ScenarioVolatilityShiftPercentagePoints.Should().Be(0);
        set.IronCondor.ExitForwardLossRatio.Should().Be(1);
        set.IronCondor.WarningForwardLossRatio.Should().Be(.8);
    }

    [Fact]
    public void Policy_roundtrips_and_its_hash_changes_when_the_limit_changes()
    {
        var set = StrategyRiskParameterSet.CreateIronCondorDevelopmentDefault();
        StrategyRiskParameterSet.Read(set.Serialize()).Should().BeEquivalentTo(set);
        var edited = set with { Version = 2, IronCondor = set.IronCondor! with { DailyLossLimit = 800 } };
        edited.Hash().Should().NotBe(set.Hash());
        StrategyRiskParameterSet.CreateIronCondorDevelopmentDefault().Hash().Should().Be(set.Hash());
    }

    [Fact]
    public void Invalid_threshold_order_and_nonfinite_scenario_are_rejected()
    {
        var policy = new IronCondorRiskParameters();
        ((Action)(() => (policy with { WarningForwardLossRatio = 1 }).Validate())).Should().Throw<ArgumentException>();
        ((Action)(() => (policy with { ScenarioVolatilityShiftPercentagePoints = double.NaN }).Validate())).Should().Throw<ArgumentException>();
        ((Action)(() => (policy with { DailyLossLimit = 0 }).Validate())).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Unknown_and_duplicate_json_settings_are_rejected()
    {
        var json = StrategyRiskParameterSet.CreateIronCondorDevelopmentDefault().Serialize();
        ((Action)(() => StrategyRiskParameterSet.Read(json.Insert(1, "\"Unexpected\":true,")))).Should().Throw<Exception>();
        ((Action)(() => StrategyRiskParameterSet.Read(json.Insert(1, "\"Version\":1,")))).Should().Throw<Exception>();
    }

    [Fact]
    public void Captured_snapshot_keeps_full_policy_and_nullable_risk_values_on_message_roundtrip()
    {
        var snapshot = new IronCondorTradePlanSnapshot
        {
            StrategyRiskParameterSet = StrategyRiskParameterSet.CreateIronCondorDevelopmentDefault(),
            DailyPnl = -400, ForwardLossRatio = 1.2, ForwardLoss = 1200, DailyLossLimit = 1000,
            Currency = "CAD", CalculationStatus = "Unavailable"
        };
        var restored = MessagePackSerializer.Deserialize<IronCondorTradePlanSnapshot>(MessagePackSerializer.Serialize(snapshot));
        restored.StrategyRiskParameterSet.Should().BeEquivalentTo(snapshot.StrategyRiskParameterSet);
        restored.ForwardLoss.Should().Be(1200);
        restored.NetGamma.Should().BeNull();
        restored.Currency.Should().Be("CAD");
    }

    [Fact]
    public void Forward_loss_warns_before_current_daily_loss_reaches_limit()
    {
        var forwardPnl = IronCondorTradePlanSnapshotCalculator.CalculateForwardDailyPnl(-400, -650, 20);
        forwardPnl.Should().Be(-1070);
        var forwardLoss = IronCondorTradePlanSnapshotCalculator.CalculateDailyLoss(forwardPnl);
        IronCondorTradePlanSnapshotCalculator.CalculateForwardLossRatio(forwardLoss, 1000).Should().Be(1.07);
        IronCondorTradePlanSnapshotCalculator.CalculateDailyLoss(-400).Should().Be(400);
    }
}
