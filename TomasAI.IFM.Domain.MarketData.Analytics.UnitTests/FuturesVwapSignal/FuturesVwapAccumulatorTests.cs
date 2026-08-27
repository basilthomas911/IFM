using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesVwapSignal;

/// <summary>Qualifies exact futures-session VWAP calculations and continuity.</summary>
public sealed class FuturesVwapAccumulatorTests
{
    static readonly DateOnly ValueDate = new(2026, 8, 26);
    static readonly Guid Epoch = Guid.Parse("22222222-2222-2222-2222-222222222222");
    static readonly FuturesVwapConfiguration Configuration = new()
    {
        ConfigurationId = "vwap-unit-v1",
        RootSymbol = "ES"
    };
    static readonly FuturesVwapSignalEntityId EntityId = new(
        "ES20260918", ValueDate, Configuration.ConfigurationId);
    static readonly DateTimeOffset SessionStart = new(2026, 8, 25, 22, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset SessionEnd = new(2026, 8, 26, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IndividualTradePricesAndSizesProduceExactWeightedValue()
    {
        var first = FuturesVwapAccumulator.ApplyLive(
            EntityId, null, Trade(1, 100m, 2), Configuration);
        var second = FuturesVwapAccumulator.ApplyLive(
            EntityId, first.Checkpoint, Trade(2, 110m, 1), Configuration);

        Assert.Equal(310m, second.Checkpoint.CumulativePriceVolume);
        Assert.Equal(3, second.Checkpoint.CumulativeVolume);
        Assert.Equal(310m / 3m, second.Signal.Vwap);
        Assert.Equal(110m - 310m / 3m, second.Signal.PriceMinusVwap);
        Assert.True(second.Signal.IsTickExact);
    }

    [Fact]
    public void DuplicateOrOlderOrdinalDoesNotAdvanceState()
    {
        var first = FuturesVwapAccumulator.ApplyLive(
            EntityId, null, Trade(2, 100m, 1), Configuration);

        var duplicate = FuturesVwapAccumulator.ApplyLive(
            EntityId, first.Checkpoint, Trade(2, 101m, 1), Configuration);

        Assert.False(duplicate.Changed);
        Assert.Equal(first.Checkpoint, duplicate.Checkpoint);
    }

    [Fact]
    public void ForwardOrdinalGapIncludesKnownTradeButInvalidatesExactSignal()
    {
        var first = FuturesVwapAccumulator.ApplyLive(
            EntityId, null, Trade(1, 100m, 1), Configuration);

        var gap = FuturesVwapAccumulator.ApplyLive(
            EntityId, first.Checkpoint, Trade(3, 102m, 1), Configuration);

        Assert.False(gap.Signal.IsValid);
        Assert.False(gap.Signal.IsTickExact);
        Assert.Equal(FuturesVwapInvalidReason.DeliveryGap, gap.Signal.InvalidReason);
        Assert.Equal(2, gap.Checkpoint.EligibleTradeCount);
    }

    [Fact]
    public void StreamEpochChangeInvalidatesExactSignal()
    {
        var first = FuturesVwapAccumulator.ApplyLive(
            EntityId, null, Trade(1, 100m, 1), Configuration);
        var changed = Trade(2, 101m, 1) with { StreamEpochId = Guid.NewGuid() };

        var result = FuturesVwapAccumulator.ApplyLive(
            EntityId, first.Checkpoint, changed, Configuration);

        Assert.Equal(FuturesVwapInvalidReason.StreamEpochChanged, result.Signal.InvalidReason);
        Assert.False(result.Signal.IsValid);
    }

    [Theory]
    [InlineData(FuturesVwapTradeAction.Change)]
    [InlineData(FuturesVwapTradeAction.Cancel)]
    [InlineData(FuturesVwapTradeAction.Correct)]
    [InlineData(FuturesVwapTradeAction.Clear)]
    public void UncorrelatableCorrectionActionsInvalidateSignal(FuturesVwapTradeAction action)
    {
        var first = FuturesVwapAccumulator.ApplyLive(
            EntityId, null, Trade(1, 100m, 1), Configuration);

        var result = FuturesVwapAccumulator.ApplyLive(
            EntityId, first.Checkpoint, Trade(2, 100m, 1) with { Action = action }, Configuration);

        Assert.Equal(FuturesVwapInvalidReason.UncorrelatableCorrection, result.Signal.InvalidReason);
        Assert.False(result.Signal.IsValid);
    }

    [Fact]
    public void CompletedPrivateReplayMatchesUninterruptedLiveAccumulator()
    {
        FuturesVwapCheckpoint? live = null;
        foreach (var trade in new[] { Trade(1, 100m, 2), Trade(2, 102m, 3), Trade(3, 99m, 1) })
            live = FuturesVwapAccumulator.ApplyLive(EntityId, live, trade, Configuration).Checkpoint;
        var recovery = FuturesVwapAccumulator.ApplyRecovery(
            EntityId, null, Guid.NewGuid(), 0, true, true,
            new[] { Trade(1, 100m, 2), Trade(2, 102m, 3), Trade(3, 99m, 1) }, Configuration);

        Assert.Equal(live!.CumulativePriceVolume, recovery.Checkpoint.CumulativePriceVolume);
        Assert.Equal(live.CumulativeVolume, recovery.Checkpoint.CumulativeVolume);
        Assert.Equal(live.EligibleTradeCount, recovery.Checkpoint.EligibleTradeCount);
        Assert.True(recovery.Signal.IsValid);
        Assert.True(recovery.Signal.IsTickExact);
    }

    [Fact]
    public void PartialRecoveryRemainsExplicitlyInvalid()
    {
        var recovery = FuturesVwapAccumulator.ApplyRecovery(
            EntityId, null, Guid.NewGuid(), 0, true, false,
            new[] { Trade(1, 100m, 2) }, Configuration);

        Assert.True(recovery.Checkpoint.IsRecovering);
        Assert.False(recovery.Signal.IsValid);
        Assert.Equal(FuturesVwapInvalidReason.RecoveryIncomplete, recovery.Signal.InvalidReason);
    }

    [Fact]
    public void IdentityHasNoTimeframeAndSeparatesValueDateSessions()
    {
        Assert.True(FuturesVwapSignalEntityId.TryParse(EntityId.Format(), out var parsed));
        Assert.Equal(EntityId, parsed);
        Assert.NotEqual(EntityId, EntityId with { ValueDate = ValueDate.AddDays(1) });
        Assert.DoesNotContain(typeof(FuturesVwapSignalEntityId).GetProperties(),
            property => property.Name.Contains("TimeFrame", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RealtimeActorContainsNoVwapCalculationState()
    {
        var fields = typeof(FuturesVwapSignalRealtimeActor).GetFields(
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);
        Assert.DoesNotContain(fields, field =>
            field.FieldType == typeof(FuturesVwapCheckpoint)
            || field.FieldType == typeof(FuturesVwapSignalReadModel));
    }

    static FuturesVwapTradeObservation Trade(long ordinal, decimal price, long size) => new()
    {
        ContractId = EntityId.ContractId,
        ValueDate = ValueDate,
        Price = price,
        Size = size,
        SourceSequence = ordinal * 10,
        EventTimestampUtc = SessionStart.AddMinutes(ordinal),
        Action = FuturesVwapTradeAction.New,
        StreamEpochId = Epoch,
        TradeOrdinal = ordinal,
        SessionStartUtc = SessionStart,
        SessionEndUtc = SessionEnd
    };
}
