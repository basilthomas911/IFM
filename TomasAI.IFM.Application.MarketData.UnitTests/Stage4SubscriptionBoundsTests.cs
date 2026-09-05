using System.Diagnostics;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Subscriptions;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using Xunit.Abstractions;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

/// <summary>Short isolated allocation qualification, not elapsed live soak or a trading latency budget.</summary>
public sealed class Stage4SubscriptionBoundsTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Maximum_shared_chain_ownership_has_bounded_final_mutation_allocation()
    {
        var time = new FrozenTime();
        await using var coordinator = new MarketDataSubscriptionCoordinator("account", "GLBX.MDP3", new(2026, 9, 4),
            timeProvider: time);
        Assert.True(await coordinator.SetAvailabilityAsync(SubscriptionDatasetAvailability.Open));
        var underlying = new SubscriptionTickerKey("databento", "GLBX.MDP3", "ES", "mbp-1", SubscriptionAssetKind.Futures);
        var chain = new SubscriptionChainKey(underlying, new(2026, 9, 18), new(2026, 9, 4),
            Enumerable.Range(0, 512).Select(i => new SubscriptionTickerKey("databento", "GLBX.MDP3", $"option-{i}",
                "mbp-1", SubscriptionAssetKind.FuturesOption)));
        var target = new SubscriptionTarget(chain);
        for (var batch = 0; batch < 78; batch++)
        {
            var result = await coordinator.AcquireBatchAsync(Batch(batch, 128));
            Assert.Equal(SubscriptionResultCode.DesiredAccepted, result.Code);
        }
        Assert.Equal(9_984, coordinator.Current.Leases.Count);

        var before = GC.GetTotalAllocatedBytes(precise: true);
        var stopwatch = Stopwatch.StartNew();
        var final = await coordinator.AcquireBatchAsync(Batch(78, 16));
        stopwatch.Stop();
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - before;
        output.WriteLine($"10,000 shared-chain leases / 513 unique routes: final mutation {stopwatch.Elapsed.TotalMilliseconds:F2} ms; process allocated {allocated:N0} bytes.");
        Assert.Equal(SubscriptionResultCode.DesiredAccepted, final.Code);
        Assert.Equal(10_000, coordinator.Current.Leases.Count);
        Assert.Equal(513, coordinator.Current.Routes.Count);
        Assert.All(coordinator.Current.Routes, route => Assert.Equal(10_000, route.EffectiveOwners));
        // This algorithmic ceiling detects reintroducing a >5-million-entry flattened chain/owner
        // intermediate. The test is also run alone for measured evidence free from parallel suites.
        Assert.True(allocated < 32L * 1024 * 1024, $"Final shared-chain mutation allocated {allocated:N0} bytes.");
        Assert.Equal(SubscriptionResultCode.CapacityExceeded, (await coordinator.AcquireBatchAsync(Batch(79, 1))).Code);

        time.Advance(TimeSpan.FromSeconds(120));
        await coordinator.SweepAsync();
        Assert.Empty(coordinator.Current.Leases);
        Assert.Empty(coordinator.Current.Routes);

        SubscriptionAcquireBatchRequest Batch(int ownerNumber, int size)
        {
            var owner = new SubscriptionOwnerKey("account", new TickerStreamOwner("load", ownerNumber.ToString(), "batch"));
            return new(Guid.CreateVersion7(time.GetUtcNow()), coordinator.HostEpochId, Guid.NewGuid(), owner,
                Enumerable.Range(0, size).Select(i => new SubscriptionLeaseSelection(
                    new("account", new TickerStreamOwner("load", ownerNumber.ToString(), i.ToString())), target)),
                SubscriptionLeasePurpose.Discovery, time.GetUtcNow().AddSeconds(10));
        }
    }

    sealed class FrozenTime : TimeProvider
    {
        long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero).AddTicks(timestamp);
        public void Advance(TimeSpan value) => timestamp += value.Ticks;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new NoTimer();
    }
    sealed class NoTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
