using TomasAI.IFM.Application.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class ResidentActorCacheTests
{
    [Fact]
    public async Task Exported_occupancy_returns_to_zero_after_invalidation_and_drain()
    {
        var values = new System.Collections.Concurrent.ConcurrentDictionary<string, long>();
        using var listener = new System.Diagnostics.Metrics.MeterListener();
        listener.InstrumentPublished = (instrument, meter) =>
        {
            if (instrument.Meter.Name == "TomasAI.IFM.ResidentActors") meter.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            var mine = false;
            foreach (var tag in tags) if (tag.Key == "actor" && Equals(tag.Value, "TelemetryQualification")) mine = true;
            if (mine) values.AddOrUpdate(instrument.Name, value, (_, previous) => previous + value);
        });
        listener.Start();
        var cache = new ResidentActorCache<string>("TelemetryQualification", Policy(1));
        using (var owner = await cache.AcquireAsync(Key("metrics"), true, default)) owner!.Value = "committed";
        Assert.Equal(1, values["resident_actor.cache.entries"]);
        Assert.Equal(0, values["resident_actor.cache.pinned"]);
        using (var owner = await cache.AcquireAsync(Key("metrics"), true, default)) owner!.Invalidate();
        using (var owner = await cache.AcquireAsync(Key("drain"), true, default)) owner!.Value = "committed";
        await cache.DrainAsync();
        Assert.Equal(0, values["resident_actor.cache.entries"]);
        Assert.Equal(0, values["resident_actor.cache.pinned"]);
        Assert.Equal(4, values["resident_actor.cache.operations"]); // miss, hit, invalidation, new miss
    }

    static ActorThreadId Key(string name) => new(ActorType.Command, "ResidentPilot", name);
    static ResidentActorPolicy Policy(int maximum = 2) => new() { Enabled = true, MaximumResidentStreams = maximum };

    [Fact]
    public async Task Warm_key_reuses_value_and_counts_one_load()
    {
        var cache = new ResidentActorCache<string>("pilot", Policy());
        using (var first = await cache.AcquireAsync(Key("a"), true, default)) first!.Value = "committed";
        using (var second = await cache.AcquireAsync(Key("a"), true, default)) Assert.Equal("committed", second!.Value);
        Assert.Equal(1, cache.Statistics.Misses);
        Assert.Equal(1, cache.Statistics.Hits);
    }

    [Fact]
    public async Task Busy_capacity_falls_back_without_exceeding_the_key_limit()
    {
        var cache = new ResidentActorCache<string>("pilot", Policy(1));
        using var pinned = await cache.AcquireAsync(Key("a"), true, default);
        Assert.Null(await cache.AcquireAsync(Key("b"), true, default));
        Assert.Equal(1, cache.Statistics.Entries);
        Assert.Equal(1, cache.Statistics.PinnedEntries);
        Assert.Equal(1, cache.Statistics.Fallbacks);
    }

    [Fact]
    public async Task Same_key_waits_until_the_first_owner_is_released()
    {
        var cache = new ResidentActorCache<string>("pilot", Policy());
        var first = await cache.AcquireAsync(Key("a"), true, default);
        first!.Value = "committed";
        var waiting = cache.AcquireAsync(Key("a"), true, default).AsTask();
        Assert.False(waiting.IsCompleted);
        first.Dispose();
        using var second = await waiting.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("committed", second!.Value);
    }

    [Fact]
    public async Task Cancellation_does_not_steal_a_pinned_owner()
    {
        var cache = new ResidentActorCache<string>("pilot", Policy());
        using var first = await cache.AcquireAsync(Key("a"), true, default);
        using var cancellation = new CancellationTokenSource();
        var waiting = cache.AcquireAsync(Key("a"), true, cancellation.Token).AsTask();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Assert.Equal(1, cache.Statistics.PinnedEntries);
    }

    [Fact]
    public async Task Invalidated_state_is_never_reused_or_repopulated_by_late_work()
    {
        var cache = new ResidentActorCache<string>("pilot", Policy());
        using (var first = await cache.AcquireAsync(Key("a"), true, default))
        {
            first!.Value = "old";
            first.Invalidate();
            first.Value = "late";
            Assert.Null(first.Value);
        }
        using var next = await cache.AcquireAsync(Key("a"), true, default);
        Assert.Null(next!.Value);
        Assert.Equal(1, cache.Statistics.Invalidations);
    }

    [Fact]
    public async Task Timed_out_stage_keeps_the_same_key_pinned_until_it_settles()
    {
        var cache = new ResidentActorCache<string>("pilot", Policy());
        var late = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = await cache.AcquireAsync(Key("a"), true, default);
        first!.Invalidate();
        first.HoldUntil(late.Task);
        first.Dispose();
        var next = cache.AcquireAsync(Key("a"), true, default).AsTask();
        Assert.False(next.IsCompleted);
        Assert.Equal(1, cache.Statistics.PinnedEntries);
        late.SetException(new InvalidOperationException("late failure"));
        using var owner = await next.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Null(owner!.Value);
    }

    [Fact]
    public async Task Disabling_a_policy_waits_for_pending_work_then_uses_the_standard_path()
    {
        var policy = Policy();
        var cache = new ResidentActorCache<string>("pilot", policy);
        var first = await cache.AcquireAsync(Key("a"), true, default);
        first!.Value = "committed";
        policy.Enabled = false;
        var next = cache.AcquireAsync(Key("a"), false, default).AsTask();
        Assert.False(next.IsCompleted);
        first.Dispose();
        Assert.Null(await next.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(0, cache.Statistics.Entries);
    }

    [Fact]
    public async Task Idle_expiry_reloads_instead_of_reusing_an_old_snapshot()
    {
        var clock = new Clock();
        var policy = Policy();
        var cache = new ResidentActorCache<string>("pilot", policy, clock);
        using (var first = await cache.AcquireAsync(Key("a"), true, default)) first!.Value = "old";
        clock.Now += policy.IdleTimeout;
        using var next = await cache.AcquireAsync(Key("a"), true, default);
        Assert.Null(next!.Value);
        Assert.Equal(1, cache.Statistics.Evictions);
    }

    [Fact]
    public async Task New_key_evicts_an_idle_owner_but_never_a_pinned_one()
    {
        var cache = new ResidentActorCache<string>("pilot", Policy(1));
        using (var first = await cache.AcquireAsync(Key("a"), true, default)) first!.Value = "a";
        using (var second = await cache.AcquireAsync(Key("b"), true, default)) second!.Value = "b";
        Assert.Equal(1, cache.Statistics.Entries);
        Assert.Equal(1, cache.Statistics.Evictions);
        using var reloaded = await cache.AcquireAsync(Key("a"), true, default);
        Assert.Null(reloaded!.Value);
    }

    [Fact]
    public async Task Shutdown_drains_pinned_work_then_clears_resident_state()
    {
        var cache = new ResidentActorCache<string>("pilot", Policy());
        var first = await cache.AcquireAsync(Key("a"), true, default);
        first!.Value = "committed";
        var drain = cache.DrainAsync().AsTask();
        Assert.False(drain.IsCompleted);
        first.Dispose();
        await drain.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(0, cache.Statistics.Entries);
    }

    [Fact]
    public void New_pilots_are_disabled_independently_and_validate_their_bounds()
    {
        var options = new ResidentActorPilotOptions().Validate();
        Assert.False(options.Vwap.Enabled);
        Assert.False(options.IronCondorTradePlan.Enabled);
        options.Vwap.Enabled = true;
        Assert.False(options.MarketOutlook.Enabled);
        options.FuturesTradePlan.MaximumResidentStreams = 0;
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
