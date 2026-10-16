using TomasAI.IFM.Shared.EventModelActor;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class RealtimeCompletionOptimizationTests
{
    [Fact]
    public void Disabled_fastpath_and_armed_matching_records_preserve_exact_counts()
    {
        var generation = Guid.NewGuid();
        var subject = new ActorSubject(ActorType.Realtime, "RecoveryProbe", "Tick", "ES");
        var evidence = new RealtimeActorCompletionEvidence();
        evidence.Record("GLBX.MDP3", generation, subject);
        Assert.Null(evidence.Capture().Target);
        evidence.Arm(new("GLBX.MDP3", generation, ActorType.Realtime, "RecoveryProbe", "Tick"));
        Parallel.For(0, 10000, _ => evidence.Record("GLBX.MDP3", generation, subject));
        Assert.Equal(10000, evidence.Capture().Completed);
        evidence.Disarm();
        evidence.Record("GLBX.MDP3", generation, subject);
        Assert.Equal(0, evidence.Capture().Completed);
        var replacement = Guid.NewGuid();
        evidence.Arm(new("GLBX.MDP3", replacement, ActorType.Realtime, "RecoveryProbe", "Tick"));
        evidence.Record("GLBX.MDP3", generation, subject);
        evidence.Record("other", replacement, subject);
        Assert.Equal(0, evidence.Capture().Completed);
        evidence.Record("GLBX.MDP3", replacement, subject);
        Assert.Equal(1, evidence.Capture().Completed);
    }

    [Fact]
    public async Task Rearm_resets_completed_records_even_when_it_races_an_inflight_record()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var clock = new BlockingClock(entered, release);
        var evidence = new RealtimeActorCompletionEvidence(clock);
        var old = Guid.NewGuid(); var next = Guid.NewGuid();
        var subject = new ActorSubject(ActorType.Realtime, "RecoveryProbe", "Tick", "ES");
        evidence.Arm(new("GLBX.MDP3", old, ActorType.Realtime, "RecoveryProbe", "Tick"));
        var record = Task.Run(() => evidence.Record("GLBX.MDP3", old, subject));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            var rearm = Task.Run(() => evidence.Arm(new("GLBX.MDP3", next, ActorType.Realtime, "RecoveryProbe", "Tick")));
            release.Set();
            await Task.WhenAll(record, rearm).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, evidence.Capture().Completed);
            evidence.Record("GLBX.MDP3", old, subject);
            Assert.Equal(0, evidence.Capture().Completed);
            evidence.Record("GLBX.MDP3", next, subject);
            Assert.Equal(1, evidence.Capture().Completed);
        }
        finally { release.Set(); }
    }

    sealed class BlockingClock(ManualResetEventSlim entered, ManualResetEventSlim release) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Test clock was not released.");
            return DateTimeOffset.UtcNow;
        }
    }
}
