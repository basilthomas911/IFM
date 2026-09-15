using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesRsiSignal;

/// <summary>Checks that RSI generation facts are accepted and replay without rejected writes.</summary>
public sealed class FuturesRsiEventApplicationTests
{
    [Fact]
    public void DailyCollectionIsPendingAndCanReplayAfterTheDailySignal()
    {
        var signal = new FuturesRsiSignalReadModel { RSI = 55d };
        var signalEvent = new FuturesRsiDailySignalGeneratedEvent { FuturesRsiSignal = signal };
        var collectionEvent = new FuturesRsiDailySignalsGeneratedEvent
        {
            FuturesRsiSignals = [signal], PeriodLength = 14
        };
        var state = new FuturesRsiSignalCommandState();

        Assert.True(state.Update(signalEvent));
        Assert.True(state.Update(collectionEvent));
        Assert.Equal(2, state.Events.Count);

        var restored = new FuturesRsiSignalCommandState();
        restored.ReplayEvents(state.Events.ToArray());
        Assert.Single(restored.FuturesRsiSignals);
        Assert.Empty(restored.Events);
    }

    [Fact]
    public void RejectedSignalCannotChangeTheAccumulatorOrEnterThePendingBatch()
    {
        var state = new FuturesRsiSignalCommandState();
        var checkpoint = new FuturesRsiAccumulatorCheckpoint();
        var invalid = new FuturesRsiSignalGeneratedEvent
        {
            FuturesRsiSignal = null!, AccumulatorCheckpoint = checkpoint
        };

        Assert.False(state.Update(invalid));
        Assert.Null(state.AccumulatorCheckpoint);
        Assert.Empty(state.FuturesRsiSignals);
        Assert.Empty(state.Events);
        Assert.False(state.Updated);
    }
}
