using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.Architecture;

public sealed class AnalyticsCommandInitializationConventionTests
{
    static readonly DateOnly ValueDate = new(2026, 10, 5);

    [Fact]
    public void Rejected_atr_seed_preserves_existing_checkpoint_signal_and_pending_events()
    {
        var state = new FuturesAtrSignalCommandState();
        var id = new FuturesAtrSignalId("ES", ValueDate, TimeFrameType.FiveMinutes, 14, new(12, 0));
        Assert.True(new GenerateFuturesAtrSignalCommand(id, 100, Bar(), false).Execute(state).Success);
        var checkpoint = state.FuturesAtrCheckpoint;
        var signal = state.AtrSignal;
        var pending = state.Events.ToArray();
        var start = new StartFuturesAtrSignalCommand(id.ToEntityId()) { CommandId = Guid.NewGuid(), HistoricalSeed = [Bar()] };
        Assert.False(start.Execute(state).Success);
        Assert.Same(checkpoint, state.FuturesAtrCheckpoint);
        Assert.Same(signal, state.AtrSignal);
        Assert.Equal(pending, state.Events.ToArray());
    }

    [Fact]
    public void Rejected_macd_seed_preserves_existing_history_and_pending_events()
    {
        var state = new FuturesMacdSignalCommandState();
        var command = SampleData.MacdGenerateCommand with { CommandId = Guid.NewGuid() };
        Assert.True(command.Execute(state).Success);
        var signals = state.MacdSignals.ToArray();
        var pending = state.Events.ToArray();
        var start = new StartFuturesMacdSignalCommand(command.EntityId) { CommandId = Guid.NewGuid(), HistoricalSeed = [Bar()] };
        Assert.False(start.Execute(state).Success);
        Assert.Equal(signals, state.MacdSignals.ToArray());
        Assert.Equal(pending, state.Events.ToArray());
    }

    [Fact]
    public void Legacy_ema_event_payload_deserializes_with_business_property_names()
    {
        var entityId = new FuturesTradeSessionBarEntityId(MarketSeriesIdentity.ForContract("ES"), TimeFrameType.FiveMinutes);
        var legacy = new LegacyEmaEvent
        {
            CommandId = Guid.NewGuid(), EntityId = entityId,
            Subject = new(ActorType.Event, FuturesEmaSignalGeneratedEvent.Actor, FuturesEmaSignalGeneratedEvent.Verb, entityId.Format()),
            Signal = new() { Price = 100, Ema10 = 99 },
            Observation = Bar(), Checkpoint = new() { Count = 18, Ema10 = 99 }
        };
        var restored = MessagePackSerializer.Deserialize<FuturesEmaSignalGeneratedEvent>(MessagePackSerializer.Serialize(legacy));
        Assert.Equal(legacy.CommandId, restored.CommandId);
        Assert.Equal(legacy.Signal.Price, restored.FuturesEmaSignal.Price);
        Assert.Equal(legacy.Signal.Ema10, restored.FuturesEmaSignal.Ema10);
        Assert.Equal(legacy.Checkpoint.Count, restored.FuturesEmaCheckpoint.Count);
        Assert.Equal(legacy.Observation.Close, restored.Observation.Close);
    }

    static FuturesTradeSessionBarReadModel Bar() => new()
    {
        ContractId = "ES", ValueDate = ValueDate, TimeFrame = TimeFrameType.FiveMinutes,
        MarketSeriesIdentity = MarketSeriesIdentity.ForContract("ES"),
        IntervalStartUtc = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero),
        IntervalEndUtc = new(2026, 10, 5, 12, 5, 0, TimeSpan.Zero),
        LastMarketEventUtc = new(2026, 10, 5, 12, 4, 59, TimeSpan.Zero),
        Close = 100, High = 101, Low = 99, IsComplete = true, IsValid = true
    };

    // Permanent pre-rename wire schema: numeric keys, not CLR property names, carry compatibility.
    [MessagePackObject]
    public sealed class LegacyEmaEvent
    {
        [Key(0)] public ActorSubject Subject { get; init; }
        [Key(1)] public Guid Id { get; init; }
        [Key(2)] public FuturesTradeSessionBarEntityId EntityId { get; init; } = default!;
        [Key(3)] public long EventId { get; init; }
        [Key(4)] public Guid CommandId { get; init; }
        [Key(5)] public string AggregateId { get; init; } = "";
        [Key(6)] public string EventSource { get; init; } = "";
        [Key(7)] public DateTime ReceivedOn { get; init; }
        [Key(8)] public FuturesEmaSignalReadModel Signal { get; init; } = new();
        [Key(9)] public FuturesTradeSessionBarReadModel Observation { get; init; } = new();
        [Key(10)] public FuturesEmaAccumulatorCheckpoint Checkpoint { get; init; } = new();
    }
}
