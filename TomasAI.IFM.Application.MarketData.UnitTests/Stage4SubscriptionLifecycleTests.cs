using System.Diagnostics;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Subscriptions;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

/// <summary>Offline pump/timer lifecycle evidence only; no provider, persistence or authorization adapter.</summary>
public sealed class Stage4SubscriptionLifecycleTests
{
    private static readonly DateOnly ValueDate = new(2026, 9, 4);

    [Fact]
    public async Task Automatic_timer_expires_ephemeral_lease_without_an_explicit_sweep_command()
    {
        var time = new ManualLeaseTime();
        await using var coordinator = await OpenAsync(time);
        var acquired = await coordinator.AcquireAsync(Acquire(time, coordinator, "one"));
        Assert.Equal(SubscriptionResultCode.DesiredAccepted, acquired.Code);
        Assert.Equal(TimeSpan.FromSeconds(15), time.Timer.DueTime);
        Assert.Equal(TimeSpan.FromSeconds(15), time.Timer.Period);

        time.Advance(TimeSpan.FromSeconds(120));
        time.Timer.Trigger();

        // Do not use SweepAsync/SetAvailabilityAsync as a barrier: both themselves
        // sweep expired leases and could hide a broken automatic timer callback.
        await ObserveAsync(() => coordinator.Current.Leases.Count == 0);
        Assert.Empty(coordinator.Current.Routes);
        Assert.Equal(acquired.DesiredRevision + 1, coordinator.Current.Revision);
    }

    [Fact]
    public async Task Timer_callbacks_coalesce_behind_a_busy_pump_and_expire_current_intent()
    {
        var time = new ManualLeaseTime();
        await using var coordinator = await OpenAsync(time, new() { CommandCapacity = 1 });
        var acquired = await coordinator.AcquireAsync(Acquire(time, coordinator, "one"));
        Assert.Equal(SubscriptionResultCode.DesiredAccepted, acquired.Code);

        using var clockGate = time.BlockNextTimestamp();
        var barrier = coordinator.SweepAsync();
        await clockGate.Entered.WaitAsync(TimeSpan.FromSeconds(5));
        time.Advance(TimeSpan.FromSeconds(120));
        for (var index = 0; index < 100; index++) time.Timer.Trigger();
        clockGate.Release();

        Assert.True(await barrier.WaitAsync(TimeSpan.FromSeconds(5)));
        await ObserveAsync(() => coordinator.Current.Leases.Count == 0);
        // Expiration is one transition, not one transition per timer callback.
        Assert.Equal(acquired.DesiredRevision + 1, coordinator.Current.Revision);
    }

    [Fact]
    public async Task Full_command_queue_rejects_new_work_and_cancellation_removes_no_existing_lease()
    {
        var time = new ManualLeaseTime();
        await using var coordinator = await OpenAsync(time, new() { CommandCapacity = 1 });
        var firstRequest = Acquire(time, coordinator, "first");
        using var clockGate = time.BlockNextTimestamp();
        var first = coordinator.AcquireAsync(firstRequest);
        await clockGate.Entered.WaitAsync(TimeSpan.FromSeconds(5));

        using var cancellation = new CancellationTokenSource();
        var queued = coordinator.AcquireAsync(Acquire(time, coordinator, "queued"), cancellation.Token);
        var refused = await coordinator.AcquireAsync(Acquire(time, coordinator, "refused"));
        Assert.Equal(SubscriptionResultCode.CapacityExceeded, refused.Code);
        cancellation.Cancel();
        clockGate.Release();

        Assert.Equal(SubscriptionResultCode.DesiredAccepted,
            (await first.WaitAsync(TimeSpan.FromSeconds(5))).Code);
        Assert.Equal(SubscriptionResultCode.Cancelled,
            (await queued.WaitAsync(TimeSpan.FromSeconds(5))).Code);
        var remaining = Assert.Single(coordinator.Current.Leases);
        Assert.Equal(firstRequest.Owner, remaining.Owner);
    }

    [Fact]
    public async Task Commands_that_expire_while_queued_return_timeout_without_late_acquisition()
    {
        var time = new ManualLeaseTime();
        await using var coordinator = await OpenAsync(time, new() { CommandCapacity = 1 });
        using var clockGate = time.BlockNextTimestamp();
        var first = coordinator.AcquireAsync(Acquire(time, coordinator, "first"));
        await clockGate.Entered.WaitAsync(TimeSpan.FromSeconds(5));
        var queued = coordinator.AcquireAsync(Acquire(time, coordinator, "queued"));

        time.Advance(TimeSpan.FromSeconds(11));
        clockGate.Release();

        Assert.Equal(SubscriptionResultCode.Timeout,
            (await first.WaitAsync(TimeSpan.FromSeconds(5))).Code);
        Assert.Equal(SubscriptionResultCode.Timeout,
            (await queued.WaitAsync(TimeSpan.FromSeconds(5))).Code);
        Assert.Empty(coordinator.Current.Leases);
        Assert.Equal(0, coordinator.Current.Revision);
    }

    [Fact]
    public async Task Acquire_crossing_deadline_after_initial_validation_does_not_commit_prepared_intent()
    {
        var time = new ManualLeaseTime();
        await using var coordinator = await OpenAsync(time);
        var request = Acquire(time, coordinator, "deadline-crossing");

        // The first three reads are the entry expiry sweep, sweep admission clock,
        // and Execute's initial deadline validation. Advance only when execution
        // next samples time during preparation/commit, not while the command queues.
        time.AdvanceOnTimestampRead(4, TimeSpan.FromSeconds(11));
        var result = await coordinator.AcquireAsync(request);

        Assert.True(time.TimestampAdvanceTriggered);
        Assert.Equal(SubscriptionResultCode.Timeout, result.Code);
        Assert.Null(result.Lease);
        Assert.Empty(result.SelectedLeases);
        Assert.Empty(coordinator.Current.Leases);
        Assert.Empty(coordinator.Current.Routes);
        Assert.Equal(0, coordinator.Current.Revision);
    }

    [Fact]
    public async Task Renewal_crossing_exact_ttl_after_initial_validation_cannot_resurrect_the_lease()
    {
        var time = new ManualLeaseTime();
        await using var coordinator = await OpenAsync(time);
        var original = (await coordinator.AcquireAsync(Acquire(time, coordinator, "renewal-crossing"))).Lease!;
        time.Advance(TimeSpan.FromSeconds(119));
        var issued = DateTimeOffset.FromUnixTimeMilliseconds(time.GetUtcNow().ToUnixTimeMilliseconds());
        var request = new SubscriptionRenewRequest(Guid.CreateVersion7(issued), Guid.NewGuid(),
            original.Owner, original.Token, issued.AddSeconds(10));

        // The initial sweep sees age119 and the command deadline stays valid.
        // At the final execution sample, age reaches TTL120 exactly.
        time.AdvanceOnTimestampRead(4, TimeSpan.FromSeconds(1));
        var result = await coordinator.RenewAsync(request);

        Assert.True(time.TimestampAdvanceTriggered);
        Assert.Equal(SubscriptionResultCode.Expired, result.Code);
        Assert.Null(result.Lease);
        Assert.DoesNotContain(coordinator.Current.Leases, lease =>
            lease.Token.LeaseId == original.Token.LeaseId && lease.Token.Version > original.Token.Version);
        Assert.True(await coordinator.SweepAsync());
        Assert.Empty(coordinator.Current.Leases);
        Assert.Empty(coordinator.Current.Routes);
    }

    [Fact]
    public async Task Dispose_drains_cancelled_queued_work_and_is_idempotent_without_timer_leaks()
    {
        var time = new ManualLeaseTime();
        await using var coordinator = await OpenAsync(time, new() { CommandCapacity = 1 });
        using var clockGate = time.BlockNextTimestamp();
        var inFlight = coordinator.AcquireAsync(Acquire(time, coordinator, "in-flight"));
        await clockGate.Entered.WaitAsync(TimeSpan.FromSeconds(5));
        var queued = coordinator.AcquireAsync(Acquire(time, coordinator, "queued"));

        var dispose = coordinator.DisposeAsync().AsTask();
        Assert.True(time.Timer.IsDisposed);
        Assert.False(dispose.IsCompleted);
        clockGate.Release();

        // A command already executing may finish; disposal must wait for it and
        // cancel commands that have not started, without orphaning their callers.
        var inFlightResult = await inFlight.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains(inFlightResult.Code,
            new[] { SubscriptionResultCode.DesiredAccepted, SubscriptionResultCode.Cancelled });
        Assert.Equal(SubscriptionResultCode.Cancelled,
            (await queued.WaitAsync(TimeSpan.FromSeconds(5))).Code);
        await dispose.WaitAsync(TimeSpan.FromSeconds(5));
        await coordinator.DisposeAsync();
        Assert.Equal(1, time.Timer.DisposeCount);

        Assert.Equal(SubscriptionResultCode.Cancelled,
            (await coordinator.AcquireAsync(Acquire(time, coordinator, "after-dispose"))).Code);
        Assert.False(await coordinator.SweepAsync());
        Assert.False(await coordinator.SetAvailabilityAsync(SubscriptionDatasetAvailability.Open));
        var terminalRevision = coordinator.Current.Revision;
        time.Advance(TimeSpan.FromMinutes(5));
        time.Timer.Trigger();
        Assert.Equal(terminalRevision, coordinator.Current.Revision);
    }

    private static async Task<MarketDataSubscriptionCoordinator> OpenAsync(
        ManualLeaseTime time, TickerLeasePolicy? policy = null)
    {
        var coordinator = new MarketDataSubscriptionCoordinator("account", "GLBX.MDP3", ValueDate, policy, time);
        Assert.True(await coordinator.SetAvailabilityAsync(SubscriptionDatasetAvailability.Open));
        return coordinator;
    }

    private static SubscriptionAcquireRequest Acquire(
        ManualLeaseTime time, MarketDataSubscriptionCoordinator coordinator, string owner)
    {
        var issued = DateTimeOffset.FromUnixTimeMilliseconds(time.GetUtcNow().ToUnixTimeMilliseconds());
        return new(Guid.CreateVersion7(issued), coordinator.HostEpochId, Guid.NewGuid(),
            new SubscriptionOwnerKey("account", new TickerStreamOwner("test", owner, "leg")),
            new SubscriptionTarget(new SubscriptionTickerKey(
                "databento", "GLBX.MDP3", "ES", "mbp-1", SubscriptionAssetKind.Futures)),
            SubscriptionLeasePurpose.Composer, issued.AddSeconds(10));
    }

    private static async Task ObserveAsync(Func<bool> condition)
    {
        var started = Stopwatch.GetTimestamp();
        while (!condition())
        {
            Assert.True(Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(5),
                "The coordinator did not publish the expected timer transition within its bounded observation window.");
            await Task.Yield();
        }
    }

    private sealed class ManualLeaseTime : TimeProvider
    {
        private long _timestamp;
        private long _utcTicks = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero).Ticks;
        private TimestampGate? _nextGate;
        private int _timestampReadCountdown;
        private long _timestampAdvanceTicks;
        private int _timestampAdvanceTriggered;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public ManualTimer Timer { get; private set; } = null!;
        public bool TimestampAdvanceTriggered => Volatile.Read(ref _timestampAdvanceTriggered) != 0;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _utcTicks), TimeSpan.Zero);
        public override long GetTimestamp()
        {
            Interlocked.Exchange(ref _nextGate, null)?.ArriveAndWait();
            if (Volatile.Read(ref _timestampReadCountdown) > 0
                && Interlocked.Decrement(ref _timestampReadCountdown) == 0)
            {
                Advance(TimeSpan.FromTicks(Interlocked.Read(ref _timestampAdvanceTicks)));
                Volatile.Write(ref _timestampAdvanceTriggered, 1);
            }
            return Interlocked.Read(ref _timestamp);
        }
        public void AdvanceOnTimestampRead(int ordinal, TimeSpan duration)
        {
            Assert.True(ordinal > 0);
            Assert.Equal(0, Volatile.Read(ref _timestampReadCountdown));
            Interlocked.Exchange(ref _timestampAdvanceTicks, duration.Ticks);
            Volatile.Write(ref _timestampAdvanceTriggered, 0);
            Volatile.Write(ref _timestampReadCountdown, ordinal);
        }
        public void Advance(TimeSpan duration)
        {
            Interlocked.Add(ref _timestamp, duration.Ticks);
            Interlocked.Add(ref _utcTicks, duration.Ticks);
        }
        public TimestampGate BlockNextTimestamp()
        {
            var gate = new TimestampGate();
            Assert.Null(Interlocked.CompareExchange(ref _nextGate, gate, null));
            return gate;
        }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Assert.Null(Timer);
            return Timer = new ManualTimer(callback, state, dueTime, period);
        }
    }

    private sealed class TimestampGate : IDisposable
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Entered => _entered.Task;
        public void ArriveAndWait()
        {
            _entered.TrySetResult();
            if (!_released.Task.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("The test did not release its controlled timestamp gate.");
        }
        public void Release() => _released.TrySetResult();
        public void Dispose() => Release();
    }

    private sealed class ManualTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) : ITimer
    {
        private int _disposed;
        private int _disposeCount;
        public TimeSpan DueTime { get; private set; } = dueTime;
        public TimeSpan Period { get; private set; } = period;
        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;
        public int DisposeCount => Volatile.Read(ref _disposeCount);
        public void Trigger()
        {
            if (!IsDisposed && DueTime != Timeout.InfiniteTimeSpan) callback(state);
        }
        public bool Change(TimeSpan nextDueTime, TimeSpan nextPeriod)
        {
            if (IsDisposed) return false;
            DueTime = nextDueTime;
            Period = nextPeriod;
            return true;
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) Interlocked.Increment(ref _disposeCount);
        }
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
