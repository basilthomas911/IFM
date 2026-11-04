using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using Xunit;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests;

/// <summary>Checks that full lifecycle replay preserves bounded calculation inputs and historical warm-up.</summary>
public sealed class CreationWindowReplayTests
{
    [Fact]
    public void Rsi_restart_reset_discards_old_samples_and_retains_only_the_configured_window()
    {
        var state = new FuturesRsiSignalCommandState();
        state.LimitReplayWindow(2);
        var signal = SampleData.AtrRsiSignals[0];
        IEvent[] facts = [new FuturesRsiSignalGeneratedEvent { FuturesRsiSignal = signal },
            new FuturesRsiSignalStartedEvent { ResetForHistoricalSeed = true },
            new FuturesRsiSignalGeneratedEvent { FuturesRsiSignal = signal },
            new FuturesRsiSignalGeneratedEvent { FuturesRsiSignal = signal },
            new FuturesRsiSignalGeneratedEvent { FuturesRsiSignal = signal },
            new FuturesRsiSignalStoppedEvent()];
        state.ReplayEvents(Rows(facts));
        Assert.Equal(2, state.FuturesRsiSignals.Count);
        Assert.Equal(6, state.CommittedStreamVersion);
        Assert.Empty(state.Events);
        state.ReplayEvents(Rows([new FuturesRsiSignalStartedEvent { ResetForHistoricalSeed = true }], 7));
        Assert.Empty(state.FuturesRsiSignals);
        Assert.Null(state.FuturesRsiCheckpoint);
    }

    [Fact]
    public void Adx_daily_and_intraday_samples_share_the_configured_window_and_restore_stop_version()
    {
        var state = new FuturesAdxSignalCommandState();
        state.LimitReplayWindow(2);
        var first = SampleData.CreateAdxSignalGeneratedEvent();
        var last = SampleData.CreateAdxSignalDownTrendEvent();
        IEvent[] facts = [new FuturesAdxSignalStartedEvent(), first, first,
            new FuturesAdxDailySignalGeneratedEvent { FuturesAdxSignal = last.FuturesAdxSignal },
            new FuturesAdxSignalStoppedEvent()];
        state.ReplayEvents(Rows(facts));
        Assert.Equal(2, state.AdxSignals.Count);
        Assert.Equivalent(last.FuturesAdxSignal, state.AdxSignal);
        Assert.Equal(5, state.CommittedStreamVersion);
        Assert.Empty(state.Events);
    }

    [Fact]
    public void Legacy_macd_without_observation_count_recovers_warmup_from_all_creation_tail_facts()
    {
        var state = new FuturesMacdSignalCommandState();
        var sample = SampleData.CreateMacdSignalGeneratedEvent();
        var facts = Enumerable.Range(0, 10).Select(_ => (IEvent)(sample with
            { FuturesMacdSignal = sample.FuturesMacdSignal with { ObservationCount = 0 } }));
        state.ReplayEvents(Rows(facts));
        Assert.True(FuturesMacdSignalCompute.Create(5800, state.MacdSignals, FuturesMacdConfiguration.Standard, out var next));
        Assert.Equal(11, next.ObservationCount);
        Assert.Equal(10, state.CommittedStreamVersion);
        Assert.Empty(state.Events);
    }

    static IEnumerable<EventStreamReadModel> Rows(IEnumerable<IEvent> facts, int firstVersion = 1) => facts.Select((fact, index) => new EventStreamReadModel
    {
        EventVersion = firstVersion + index, StreamVersion = firstVersion + index,
        EventTypeName = fact.GetType().AssemblyQualifiedName!,
        EventData = TomasAI.IFM.Application.EventSourcing.EventLogMessagePackCodec.Shared.Serialize(fact)
    });
}
