using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Shared.FuturesVwapSignal;
using TomasAI.IFM.Shared.EventModelActor;

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
            EntityId, first.FuturesVwapCheckpoint, Trade(2, 110m, 1), Configuration);

        Assert.Equal(310m, second.FuturesVwapCheckpoint.CumulativePriceVolume);
        Assert.Equal(3, second.FuturesVwapCheckpoint.CumulativeVolume);
        Assert.Equal(310m / 3m, second.FuturesVwapSignal.Vwap);
        Assert.Equal(110m - 310m / 3m, second.FuturesVwapSignal.PriceMinusVwap);
        Assert.True(second.FuturesVwapSignal.IsTickExact);
    }

    [Fact]
    public void LaterCumulativeCheckpointHealsAnIntermediateTransportDrop()
    {
        var first = SourceCheckpoint(1, 200m, 2);
        var latest = SourceCheckpoint(3, 530m, 5);
        var initial = FuturesVwapAccumulator.ApplySourceCheckpoint(EntityId, null,
            first, Configuration, SessionStart, SessionEnd);
        var healed = FuturesVwapAccumulator.ApplySourceCheckpoint(EntityId, initial.FuturesVwapCheckpoint,
            latest, Configuration, SessionStart, SessionEnd);
        var duplicate = FuturesVwapAccumulator.ApplySourceCheckpoint(EntityId, healed.FuturesVwapCheckpoint,
            first, Configuration, SessionStart, SessionEnd);

        Assert.True(healed.Changed);
        Assert.Equal(530m / 5m, healed.FuturesVwapSignal.Vwap);
        Assert.Equal(3, healed.FuturesVwapCheckpoint.LastTradeOrdinal);
        Assert.True(healed.FuturesVwapSignal.IsTickExact);
        Assert.False(duplicate.Changed);
    }

    [Fact]
    public void InvalidSourceCheckpointDoesNotClaimTickExactValue()
    {
        var result = FuturesVwapAccumulator.ApplySourceCheckpoint(EntityId, null,
            SourceCheckpoint(1, 200m, 2) with { IsValid = false },
            Configuration, SessionStart, SessionEnd);

        Assert.False(result.FuturesVwapSignal.IsValid);
        Assert.False(result.FuturesVwapSignal.IsTickExact);
    }

    [Fact]
    public void SourceCheckpointSurvivesWorkerMessagePackBoundary()
    {
        var source = SourceCheckpoint(3, 530m, 5);
        var copy = MessagePackSerializer.Deserialize<FuturesVwapSourceCheckpoint>(
            MessagePackSerializer.Serialize(source));

        Assert.Equal(source, copy);
    }

    [Fact]
    public void CheckpointCommandSurvivesMessagePackRequestBoundary()
    {
        var command = new UpdateFuturesVwapSignalCommand
        {
            CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, UpdateFuturesVwapSignalCommand.Actor,
                UpdateFuturesVwapSignalCommand.Verb, EntityId.Format()),
            EntityId = EntityId,
            Configuration = Configuration,
            SourceCheckpoint = SourceCheckpoint(3, 530m, 5),
            SessionStartUtc = SessionStart,
            SessionEndUtc = SessionEnd
        };
        var copy = MessagePackSerializer.Deserialize<UpdateFuturesVwapSignalCommand>(
            MessagePackSerializer.Serialize(command));

        Assert.Equal(command.SourceCheckpoint, copy.SourceCheckpoint);
        Assert.Equal(SessionStart, copy.SessionStartUtc);
        Assert.Equal(SessionEnd, copy.SessionEndUtc);
    }

    [Fact]
    public void CheckpointEventSurvivesWorkerPublicationBoundary()
    {
        var entity = new TickDataEntityId(EntityId.ContractId, ValueDate, AssetTypeId.Futures);
        var original = new FuturesMarketPriceUpdatedRealtimeEvent
        {
            Subject = new(ActorType.Realtime, FuturesVwapSourceCheckpoint.Actor,
                FuturesMarketPriceUpdatedRealtimeEvent.Verb, entity.Format()),
            EntityId = entity,
            VwapCheckpoint = SourceCheckpoint(3, 530m, 5)
        };
        var copy = MessagePackSerializer.Deserialize<FuturesMarketPriceUpdatedRealtimeEvent>(
            MessagePackSerializer.Serialize(original));

        Assert.Equal(original.Subject, copy.Subject);
        Assert.Equal(original.VwapCheckpoint, copy.VwapCheckpoint);
    }

    static FuturesVwapSourceCheckpoint SourceCheckpoint(long ordinal, decimal priceVolume,
        long volume) => new()
    {
        StreamEpochId = Epoch,
        RecoveryGenerationId = Guid.NewGuid(),
        LastTradeOrdinal = ordinal,
        LastTradeSourceSequence = ordinal,
        AsOfUtc = SessionStart.AddSeconds(ordinal),
        CumulativePriceVolume = priceVolume,
        CumulativeVolume = volume,
        EligibleTradeCount = ordinal,
        LastPrice = 110m,
        IsValid = true,
        IsReplayComplete = true
    };

    [Fact]
    public void DuplicateOrOlderOrdinalDoesNotAdvanceState()
    {
        var first = FuturesVwapAccumulator.ApplyLive(
            EntityId, null, Trade(2, 100m, 1), Configuration);

        var duplicate = FuturesVwapAccumulator.ApplyLive(
            EntityId, first.FuturesVwapCheckpoint, Trade(2, 101m, 1), Configuration);

        Assert.False(duplicate.Changed);
        Assert.Equal(first.FuturesVwapCheckpoint, duplicate.FuturesVwapCheckpoint);
    }

    [Fact]
    public void ForwardOrdinalGapIncludesKnownTradeButInvalidatesExactSignal()
    {
        var first = FuturesVwapAccumulator.ApplyLive(
            EntityId, null, Trade(1, 100m, 1), Configuration);

        var gap = FuturesVwapAccumulator.ApplyLive(
            EntityId, first.FuturesVwapCheckpoint, Trade(3, 102m, 1), Configuration);

        Assert.False(gap.FuturesVwapSignal.IsValid);
        Assert.False(gap.FuturesVwapSignal.IsTickExact);
        Assert.Equal(FuturesVwapInvalidReason.DeliveryGap, gap.FuturesVwapSignal.InvalidReason);
        Assert.Equal(2, gap.FuturesVwapCheckpoint.EligibleTradeCount);
    }

    [Fact]
    public void StreamEpochChangeInvalidatesExactSignal()
    {
        var first = FuturesVwapAccumulator.ApplyLive(
            EntityId, null, Trade(1, 100m, 1), Configuration);
        var changed = Trade(2, 101m, 1) with { StreamEpochId = Guid.NewGuid() };

        var result = FuturesVwapAccumulator.ApplyLive(
            EntityId, first.FuturesVwapCheckpoint, changed, Configuration);

        Assert.Equal(FuturesVwapInvalidReason.StreamEpochChanged, result.FuturesVwapSignal.InvalidReason);
        Assert.False(result.FuturesVwapSignal.IsValid);
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
            EntityId, first.FuturesVwapCheckpoint, Trade(2, 100m, 1) with { Action = action }, Configuration);

        Assert.Equal(FuturesVwapInvalidReason.UncorrelatableCorrection, result.FuturesVwapSignal.InvalidReason);
        Assert.False(result.FuturesVwapSignal.IsValid);
    }

    [Fact]
    public void CompletedPrivateReplayMatchesUninterruptedLiveAccumulator()
    {
        FuturesVwapCheckpoint? live = null;
        foreach (var trade in new[] { Trade(1, 100m, 2), Trade(2, 102m, 3), Trade(3, 99m, 1) })
            live = FuturesVwapAccumulator.ApplyLive(EntityId, live, trade, Configuration).FuturesVwapCheckpoint;
        var recovery = FuturesVwapAccumulator.ApplyRecovery(
            EntityId, null, Guid.NewGuid(), 0, true, true,
            new[] { Trade(1, 100m, 2), Trade(2, 102m, 3), Trade(3, 99m, 1) }, Configuration);

        Assert.Equal(live!.CumulativePriceVolume, recovery.FuturesVwapCheckpoint.CumulativePriceVolume);
        Assert.Equal(live.CumulativeVolume, recovery.FuturesVwapCheckpoint.CumulativeVolume);
        Assert.Equal(live.EligibleTradeCount, recovery.FuturesVwapCheckpoint.EligibleTradeCount);
        Assert.True(recovery.FuturesVwapSignal.IsValid);
        Assert.True(recovery.FuturesVwapSignal.IsTickExact);
    }

    [Fact]
    public void CompletedReplayHandsOffToFirstLiveOrdinalWithoutInvalidation()
    {
        var liveEpoch = Guid.NewGuid();
        var recovered = FuturesVwapAccumulator.ApplyRecovery(
            EntityId, null, Guid.NewGuid(), 0, true, true,
            new[] { Trade(1, 100m, 2), Trade(2, 102m, 3) }, Configuration,
            liveEpoch, 0);

        var live = FuturesVwapAccumulator.ApplyLive(
            EntityId,
            recovered.FuturesVwapCheckpoint,
            Trade(1, 104m, 1) with { StreamEpochId = liveEpoch },
            Configuration);

        Assert.True(recovered.FuturesVwapSignal.IsTickExact);
        Assert.Equal(liveEpoch, recovered.FuturesVwapCheckpoint.StreamEpochId);
        Assert.Equal(0, recovered.FuturesVwapCheckpoint.LastTradeOrdinal);
        Assert.True(live.FuturesVwapSignal.IsTickExact);
        Assert.Equal(3, live.FuturesVwapCheckpoint.EligibleTradeCount);
        Assert.Equal(1, live.FuturesVwapCheckpoint.LastTradeOrdinal);
    }

    [Fact]
    public void Delayed_old_epoch_trade_after_replay_handoff_does_not_invalidate_vwap()
    {
        var liveEpoch = Guid.NewGuid();
        var recovered = FuturesVwapAccumulator.ApplyRecovery(
            EntityId, null, Guid.NewGuid(), 0, true, true,
            [Trade(1, 100m, 2), Trade(2, 102m, 3)], Configuration, liveEpoch, 0);

        var delayed = FuturesVwapAccumulator.ApplyLive(EntityId, recovered.FuturesVwapCheckpoint,
            Trade(2, 102m, 3) with { StreamEpochId = Epoch }, Configuration);
        var current = FuturesVwapAccumulator.ApplyLive(EntityId, delayed.FuturesVwapCheckpoint,
            Trade(1, 104m, 1) with { StreamEpochId = liveEpoch }, Configuration);

        Assert.False(delayed.Changed);
        Assert.Equal(recovered.FuturesVwapCheckpoint, delayed.FuturesVwapCheckpoint);
        Assert.True(current.FuturesVwapSignal.IsTickExact);
        Assert.Equal(6, current.FuturesVwapCheckpoint.CumulativeVolume);
    }

    [Fact]
    public void PartialRecoveryRemainsExplicitlyInvalid()
    {
        var recovery = FuturesVwapAccumulator.ApplyRecovery(
            EntityId, null, Guid.NewGuid(), 0, true, false,
            new[] { Trade(1, 100m, 2) }, Configuration);

        Assert.True(recovery.FuturesVwapCheckpoint.IsRecovering);
        Assert.False(recovery.FuturesVwapSignal.IsValid);
        Assert.Equal(FuturesVwapInvalidReason.RecoveryIncomplete, recovery.FuturesVwapSignal.InvalidReason);
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
