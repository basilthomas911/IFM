using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BddTests.FuturesVwapSignal;

/// <summary>Specifies complete-session VWAP behavior from individual executed trades.</summary>
public sealed class FuturesVwapSignalCommandTests
{
    static readonly DateOnly ValueDate = new(2026, 8, 26);
    static readonly FuturesVwapConfiguration Configuration = new()
    {
        ConfigurationId = "vwap-bdd-v1",
        RootSymbol = "ES"
    };
    static readonly FuturesVwapSignalEntityId EntityId = new(
        "ES20260918", ValueDate, Configuration.ConfigurationId);
    static readonly Guid Epoch = Guid.Parse("33333333-3333-3333-3333-333333333333");
    static readonly DateTimeOffset Start = new(2026, 8, 25, 22, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset End = new(2026, 8, 26, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CompleteTradeDayUsesEveryEligibleExecutionAndNeverAQuoteSize()
    {
        var executions = new[]
        {
            Trade(1, 6500m, 3),
            Trade(2, 6501m, 2),
            Trade(3, 6499.50m, 5),
            Trade(4, 6502m, 1)
        };
        FuturesVwapCheckpoint? checkpoint = null;
        FuturesVwapSignalReadModel? signal = null;

        foreach (var execution in executions)
        {
            var transition = FuturesVwapAccumulator.ApplyLive(
                EntityId, checkpoint, execution, Configuration);
            checkpoint = transition.Checkpoint;
            signal = transition.Signal;
        }

        var expectedNumerator = executions.Sum(value => value.Price * value.Size);
        var expectedVolume = executions.Sum(value => value.Size);
        signal.Should().NotBeNull();
        signal!.CumulativePriceVolume.Should().Be(expectedNumerator);
        signal.CumulativeVolume.Should().Be(expectedVolume);
        signal.Vwap.Should().Be(expectedNumerator / expectedVolume);
        signal.EligibleTradeCount.Should().Be(executions.Length);
        signal.IsTickExact.Should().BeTrue();
    }

    [Fact]
    public void DeliveryGapStaysInvalidUntilCompletePrivateReplayRestoresExactState()
    {
        var first = FuturesVwapAccumulator.ApplyLive(
            EntityId, null, Trade(1, 6500m, 1), Configuration);
        var broken = FuturesVwapAccumulator.ApplyLive(
            EntityId, first.Checkpoint, Trade(3, 6502m, 1), Configuration);
        broken.Signal.InvalidReason.Should().Be(FuturesVwapInvalidReason.DeliveryGap);
        broken.Signal.IsValid.Should().BeFalse();

        var generation = Guid.NewGuid();
        var recovering = FuturesVwapAccumulator.ApplyRecovery(
            EntityId, broken.Checkpoint, generation, 0, true, false,
            [Trade(1, 6500m, 1), Trade(2, 6501m, 2)], Configuration);
        recovering.Signal.IsValid.Should().BeFalse();
        recovering.Signal.InvalidReason.Should().Be(FuturesVwapInvalidReason.RecoveryIncomplete);

        var restored = FuturesVwapAccumulator.ApplyRecovery(
            EntityId, recovering.Checkpoint, generation, 1, false, true,
            [Trade(3, 6502m, 1)], Configuration);
        restored.Signal.IsValid.Should().BeTrue();
        restored.Signal.IsTickExact.Should().BeTrue();
        restored.Signal.Vwap.Should().Be((6500m + 6501m * 2 + 6502m) / 4m);
    }

    static FuturesVwapTradeObservation Trade(long ordinal, decimal price, long size) => new()
    {
        ContractId = EntityId.ContractId,
        ValueDate = ValueDate,
        Price = price,
        Size = size,
        SourceSequence = ordinal * 5,
        EventTimestampUtc = Start.AddMinutes(ordinal),
        Action = FuturesVwapTradeAction.New,
        StreamEpochId = Epoch,
        TradeOrdinal = ordinal,
        SessionStartUtc = Start,
        SessionEndUtc = End
    };
}
