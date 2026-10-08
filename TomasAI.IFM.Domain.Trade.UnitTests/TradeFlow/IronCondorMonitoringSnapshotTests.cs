using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class IronCondorMonitoringSnapshotTests
{
    static readonly DateTime Now = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
    static StrategyTradePlanSnapshot Plan => new() { CalculatedAtUtc = Now, ValueDate = new(2026, 10, 7), PlanRevision = 7, TotalPnl = 42 };
    static IronCondorTradePlanInputs Inputs => new()
    {
        TradeType = TradeType.ShortIronCondor, PutForwardPrice = -8, CallForwardPrice = -2,
        ForwardLossPriceLimit = 20, HistoricalForwardLossRatios = [0.2, 0.4, 0.6],
        BaselineIdentity = "persisted-baseline/1", DistributionIdentity = "distribution/1", ObservedAtUtc = Now
    };

    [Fact]
    public void Monitoring_plan_does_not_require_durable_projection()
        => new IronCondorTradePlanUpdatedEvent().RequiresDurableProjection.Should().BeFalse();

    [Fact]
    public void Missing_inputs_remain_null_and_report_unavailable_reasons()
    {
        var snapshot = IronCondorMonitoringSnapshotCompute.Create(Plan, Guid.NewGuid(), null);
        snapshot.TradePnl.Should().Be(42);
        snapshot.ForwardPrice.Should().BeNull();
        snapshot.MScore.Should().BeNull();
        snapshot.TradeRisk.Should().BeNull();
        snapshot.IsComplete.Should().BeFalse();
        snapshot.UnavailableReasons.Should().Contain(x => x.Contains("DISTRIBUTION"));
    }

    [Fact]
    public void Captured_values_use_legacy_formulas_and_copy_the_baseline()
    {
        var inputs = Inputs;
        var snapshot = IronCondorMonitoringSnapshotCompute.Create(Plan, Guid.NewGuid(), inputs);
        snapshot.ForwardPrice.Should().Be(10);
        snapshot.ForwardLossRatio.Should().Be(0.5);
        snapshot.MScore.Should().Be(IronCondorLegacyValueInitializers.CalculateMScore(0.5, inputs.HistoricalForwardLossRatios));
        inputs.HistoricalForwardLossRatios[0] = 99;
        snapshot.IronCondorTradePlanInputs!.HistoricalForwardLossRatios[0].Should().Be(0.2);
    }

    [Fact]
    public void Stale_distribution_cannot_produce_a_risk_classification()
    {
        var snapshot = IronCondorMonitoringSnapshotCompute.Create(Plan, Guid.NewGuid(), Inputs with { ObservedAtUtc = Now.AddMinutes(-2) });
        snapshot.TradeRisk.Should().BeNull();
        snapshot.UnavailableReasons.Should().Contain(x => x.Contains("stale"));
    }

    [Fact]
    public void Snapshot_round_trips_and_legacy_payload_is_still_readable()
    {
        var original = Plan with { IronCondorTradePlanSnapshot = IronCondorMonitoringSnapshotCompute.Create(Plan, Guid.NewGuid(), Inputs) };
        var restored = MessagePackSerializer.Deserialize<StrategyTradePlanSnapshot>(MessagePackSerializer.Serialize(original));
        restored.IronCondorTradePlanSnapshot!.ForwardLossRatio.Should().Be(0.5);
        restored.IronCondorTradePlanSnapshot.SourceEventId.Should().Be(original.IronCondorTradePlanSnapshot!.SourceEventId);
        var bytes = MessagePackSerializer.Serialize(Plan);
        // Remove the additive twentieth member to reproduce the old nineteen-member wire schema.
        bytes[2] = 19;
        var legacy = MessagePackSerializer.Deserialize<StrategyTradePlanSnapshot>(bytes[..^1]);
        legacy.IronCondorTradePlanSnapshot.Should().BeNull();
        legacy.PlanRevision.Should().Be(7);
    }

    [Fact]
    public void Different_captured_inputs_change_command_and_projection_hashes()
    {
        var command = new UpdateIronCondorTradePlanCommand { IronCondorTradePlanInputs = Inputs };
        (command with { IronCondorTradePlanInputs = Inputs with { PutForwardPrice = -9 } }).Fingerprint().Should().NotBe(command.Fingerprint());
        var first = Plan with { IronCondorTradePlanSnapshot = IronCondorMonitoringSnapshotCompute.Create(Plan, Guid.Empty, Inputs) };
        var second = first with { IronCondorTradePlanSnapshot = first.IronCondorTradePlanSnapshot! with { AssetPrice = 6000 } };
        TradePlanContractIdentity.PlanHash(second).Should().NotBe(TradePlanContractIdentity.PlanHash(first));
    }

    [Fact]
    public void Missing_distribution_does_not_erase_observed_limits_or_metadata()
    {
        var inputs = Inputs with { DistributionIdentity = string.Empty, MaturityDate = new DateOnly(2026, 11, 20),
            TradeLimits = new TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeLimitReadModel
                { TradeId = 0, TradeType = TradeType.ShortIronCondor, MaxProfit = 850, MaxLoss = -500 } };
        var snapshot = IronCondorMonitoringSnapshotCompute.Create(Plan, Guid.Empty, inputs);
        snapshot.MaxProfit.Should().Be(850);
        snapshot.MaturityDate.Should().Be(new DateOnly(2026, 11, 20));
        snapshot.ForwardPrice.Should().BeNull();
        snapshot.ActionType.Should().BeNull();
    }

    [Fact]
    public void Missing_inputs_cannot_recommend_exit_from_generic_default_thresholds()
    {
        var calculated = Plan with { RequiresExit = true, State = TradePlanState.ExitRequired, Action = TradePlanAction.ExitAtMarket };
        var snapshot = IronCondorMonitoringSnapshotCompute.Create(calculated, Guid.Empty, null);
        var result = TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.UpdateIronCondorTradePlan
            .ApplyMonitoringRecommendation(calculated, snapshot);
        result.RequiresExit.Should().BeFalse();
        result.State.Should().Be(TradePlanState.CalculationFailed);
        result.Action.Should().Be(TradePlanAction.Hold);
    }
}
