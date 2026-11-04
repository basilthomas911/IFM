using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using Xunit;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests;

/// <summary>Qualifies complete checkpoints against creation plus every intervening fact.</summary>
public sealed class CreationCheckpointEquivalenceTests
{
    [Fact]
    public void Iti_checkpoint_and_hold_tail_preserve_the_full_signal()
    {
        var first = SampleData.StartOfDayEvent;
        var checkpoint = first with { FuturesItiSignal = first.FuturesItiSignal! with { TrendExtreme = 5800, TrendReversal = 5760 } };
        var tail = new FuturesItiSignalHoldTradeSetEvent { FuturesItiSignal = checkpoint.FuturesItiSignal! with { TradeState = TomasAI.IFM.Domain.MarketData.Analytics.Shared.IntrinsicTimeTradeState.Hold } };
        IEvent[] facts = [first, SampleData.HoldTradeEvent, checkpoint, tail];
        var full = new FuturesItiSignalCommandState();
        var optimized = new FuturesItiSignalCommandState();
        full.ReplayEvents(Rows(facts));
        optimized.ReplayEvents(Rows(facts).Where(row => row.StreamVersion != 2));
        Assert.Equivalent(full.FuturesItiSignal, optimized.FuturesItiSignal);
        Assert.Equal(4, optimized.CommittedStreamVersion);
        Assert.Empty(optimized.Events);
    }

    [Fact]
    public void Trade_checkpoint_preserves_all_signal_fields_and_the_next_change_decision()
    {
        var first = new FuturesTradeSignalUpdatedEvent { FuturesTradeSignal = new FuturesTradeSignalV2ReadModel { ContractId = "ESZ6", SequenceId = 1, Mean = 5700 } };
        var latest = first with { FuturesTradeSignal = first.FuturesTradeSignal! with { SequenceId = 3, Mean = 5800, TrendExtreme = 5820, TrendReversal = 5780 } };
        IEvent[] facts = [first, first with { FuturesTradeSignal = first.FuturesTradeSignal! with { SequenceId = 2 } }, latest, new FuturesItiSignalHoldTradeChangedEvent()];
        var full = new FuturesTradeSignalCommandState();
        var optimized = new FuturesTradeSignalCommandState();
        full.ReplayEvents(Rows(facts));
        optimized.ReplayEvents(Rows(facts).Where(row => row.StreamVersion != 2));
        Assert.Equivalent(full.FuturesTradeSignal, optimized.FuturesTradeSignal);
        Assert.Equal(full.HasFuturesTradeSignalChanged(latest.FuturesTradeSignal!), optimized.HasFuturesTradeSignalChanged(latest.FuturesTradeSignal!));
        Assert.Equal(4, optimized.CommittedStreamVersion);
        Assert.Empty(optimized.Events);
    }

    [Fact]
    public void Tdi_checkpoint_preserves_the_complete_model()
    {
        var first = new FuturesTdiSignalGeneratedEvent { FuturesTdiSignal = new FuturesTdiSignalReadModel { ContractId = "ESZ6", Rsi = 30, SourceSequence = 1 } };
        var latest = first with { FuturesTdiSignal = first.FuturesTdiSignal! with { SourceSequence = 3, Rsi = 65, PriceLine = 63, SignalLine = 60 } };
        IEvent[] facts = [first, first with { FuturesTdiSignal = first.FuturesTdiSignal! with { SourceSequence = 2 } }, latest];
        var full = new FuturesTdiSignalCommandState();
        var optimized = new FuturesTdiSignalCommandState();
        full.ReplayEvents(Rows(facts));
        optimized.ReplayEvents(Rows(facts).Where(row => row.StreamVersion != 2));
        Assert.Equivalent(full.TdiSignal, optimized.TdiSignal);
        Assert.Equal(3, optimized.CommittedStreamVersion);
        Assert.Empty(optimized.Events);
    }

    static IEnumerable<EventStreamReadModel> Rows(IEnumerable<IEvent> facts) => facts.Select((fact, index) => new EventStreamReadModel
    {
        EventVersion = index + 1, StreamVersion = index + 1,
        EventTypeName = fact.GetType().AssemblyQualifiedName!,
        EventData = TomasAI.IFM.Application.EventSourcing.EventLogMessagePackCodec.Shared.Serialize(fact)
    });
}
