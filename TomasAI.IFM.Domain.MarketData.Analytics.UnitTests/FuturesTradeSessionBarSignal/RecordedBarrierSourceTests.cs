using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTradeSessionBarSignal;

public sealed class RecordedBarrierSourceTests
{
    [Fact]
    public async Task Recorded_barriers_suppress_the_timer_but_keep_original_barrier_handling_available()
    {
        var live = new RealtimeMailboxSafetyFixture.Handler(); var recorded = new RealtimeMailboxSafetyFixture.Handler();
        var liveClock = new BarrierClock(false); var recordedClock = new BarrierClock(true);
        live.Context.TimeProvider.Returns(liveClock); recorded.Context.TimeProvider.Returns(recordedClock);
        live.Context.IsReady.Returns(true); recorded.Context.IsReady.Returns(true);
        var liveActor = (IEventActor<FuturesTradeSessionBarSignalRealtimeActor>)live.Actor;
        var recordedActor = (IEventActor<FuturesTradeSessionBarSignalRealtimeActor>)recorded.Actor;
        await liveActor.OnStartup(live.Context); await recordedActor.OnStartup(recorded.Context);
        try
        {
            await liveClock.Observed.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(recordedClock.Observed.Task.IsCompleted);
            var trade = RealtimeMailboxSafetyFixture.Trade(1); await recorded.Receive(trade);
            var id = recorded.Context.Accumulators.Resolve(trade.EntityId.ContractId, trade.EntityId.ValueDate);
            await recorded.ReceiveBatch([new FuturesTradeSessionBarSignalBarrierRealtimeEvent
            {
                EntityId = id, Subject = new(TomasAI.IFM.Shared.EventModelActor.ActorType.Realtime, FuturesTradeSessionBarSignalRealtimeActor.ActorName, "Barrier", id.Format()),
                BarrierUtc = new(2026, 8, 26, 0, 0, 0, TimeSpan.Zero)
            }]);
            Assert.NotEmpty(recorded.Persisted);
        }
        finally { await liveActor.OnShutdown(live.Context); await recordedActor.OnShutdown(recorded.Context); }
    }

    sealed class BarrierClock(bool recorded) : TimeProvider, TomasAI.IFM.Shared.Time.IRecordedRealtimeSourceReplay
    {
        public TaskCompletionSource Observed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool UsesRecordedSource(string actor, string verb) => recorded && actor == "FuturesTradeSessionBarSignal" && verb == "Barrier";
        public override DateTimeOffset GetUtcNow() { Observed.TrySetResult(); return new(2026, 8, 25, 18, 0, 0, TimeSpan.Zero); }
    }
}
