using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Shared.EventModelActor;
using Xunit;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class RealtimeSessionRootFenceTests
{
    [Fact]
    public async Task Hold_finishes_accepted_work_and_parks_new_work_until_same_owner_releases()
    {
        var fence=new RealtimeSessionRootFence();
        var accepted=await fence.EnterAsync(default);
        var lease=Guid.NewGuid().ToString("N");var boundary=Guid.NewGuid().ToString("N");
        fence.Hold(lease,boundary);fence.Hold(lease,boundary);
        Assert.Equal(1,fence.Snapshot().Active);
        var waiting=fence.EnterAsync(default).AsTask();
        Assert.False(waiting.IsCompleted);
        accepted.Dispose();accepted.Dispose();
        Assert.Equal(0,fence.Snapshot().Active);
        Assert.Throws<InvalidOperationException>(()=>fence.Release(Guid.NewGuid().ToString("N")));
        Assert.False(waiting.IsCompleted);
        fence.Release(lease);
        using var resumed=await waiting.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1,fence.Snapshot().Active);
        Assert.False(fence.Snapshot().Held);
    }
    [Fact]
    public async Task Cancellation_cannot_admit_a_parked_observation_or_change_owner()
    {
        var fence=new RealtimeSessionRootFence();var lease=Guid.NewGuid().ToString("N");
        fence.Hold(lease,Guid.NewGuid().ToString("N"));
        using var stopping=new CancellationTokenSource();
        var waiting=fence.EnterAsync(stopping.Token).AsTask();stopping.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>waiting);
        Assert.Equal(0,fence.Snapshot().Active);
        Assert.Throws<InvalidOperationException>(()=>fence.Hold(Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N")));
        Assert.True(fence.Snapshot().Held);
    }
    [Theory]
    [InlineData("FuturesTradeSessionBarSignal","Barrier",true)]
    [InlineData("FuturesTradePositionRealtime","EvaluateFuturesExitClock",true)]
    [InlineData("IronCondorTradePositionRealtime","IronCondorMonitoringInputsChanged",true)]
    [InlineData("FuturesTradeSessionBarSignal","Closed",false)]
    [InlineData("FuturesTradePositionRealtime","PositionChanged",false)]
    public void Fence_only_matches_exact_recorded_timer_roots(string actor,string verb,bool expected)
    {
        Assert.Equal(expected,RealtimeSessionRootFence.IsRoot(new(ActorType.Realtime,actor,verb,"original")));
        Assert.False(RealtimeSessionRootFence.IsRoot(new(ActorType.Event,actor,verb,"original")));
    }
}
