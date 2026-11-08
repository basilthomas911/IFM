using TomasAI.IFM.Application.Api.Server.Core.Observability.HealthChecks;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Application.UnitTests;

public sealed class ActorHealthSnapshotReaderTests
{
    [Fact]
    public void Concurrent_views_share_one_capture_and_expiry_and_invalidation_expose_changed_health()
    {
        var clock = new Clock();
        var calls = 0;
        var status = SupervisorActorHealthStatus.Green;
        var reader = new ActorHealthSnapshotReader(() =>
        { Interlocked.Increment(ref calls); return new(DateTime.UtcNow, status, 0, 0, 0, 0, [], [], [], []); }, clock);
        var rows = new SupervisorRuntimeSnapshot[32];
        Parallel.For(0, rows.Length, index => rows[index] = reader.Capture());
        Assert.Equal(1, calls); Assert.All(rows, row => Assert.Same(rows[0], row));
        status = SupervisorActorHealthStatus.Red;
        reader.Invalidate(); Assert.Equal(SupervisorActorHealthStatus.Red, reader.Capture().OverallStatus);
        Assert.Equal(2, calls);
        clock.Advance(TimeSpan.FromSeconds(1)); status = SupervisorActorHealthStatus.Yellow;
        Assert.Equal(SupervisorActorHealthStatus.Yellow, reader.Capture().OverallStatus);
        Assert.Equal(3, calls);
    }

    [Fact]
    public void A_failed_refresh_does_not_extend_the_old_green_snapshot()
    {
        var clock = new Clock();
        var failed = false;
        var reader = new ActorHealthSnapshotReader(() => failed ? throw new IOException("fixture")
            : new(DateTime.UtcNow, SupervisorActorHealthStatus.Green, 0, 0, 0, 0, [], [], [], []), clock);
        reader.Capture(); clock.Advance(TimeSpan.FromSeconds(1)); failed = true;
        Assert.Throws<IOException>(() => reader.Capture());
        Assert.Throws<IOException>(() => reader.Capture());
    }

    sealed class Clock : TimeProvider
    {
        long ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref ticks);
        public void Advance(TimeSpan elapsed) => Interlocked.Add(ref ticks, elapsed.Ticks);
    }
}
