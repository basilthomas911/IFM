using FluentAssertions;
using NSubstitute;
using System.Diagnostics;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Processing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit.Abstractions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.MarketOutlookSnapshot;

[Collection(MarketOutlookHotCacheTestCollection.Name)]
public sealed class MarketOutlookUpdateProcessorTests(ITestOutputHelper output)
{
    static readonly MarketOutlookEntityId Id = new("ESZ26", new DateOnly(2026, 9, 1));

    sealed record UnsupportedMarketOutlookUpdate : MarketOutlookUpdate
    {
        public override MarketOutlookUpdateKind Kind => MarketOutlookUpdateKind.VixPrice;
    }

    sealed class ThrowingOperationsRecorder : IMarketDataOperationsRecorder
    {
        public void Record(in MarketDataOperationMeasurement measurement) =>
            throw new InvalidOperationException("injected metrics failure");
    }

    sealed class CountingPublisher : IMarketOutlookSnapshotPublisher
    {
        long count;
        public long Count => Interlocked.Read(ref count);

        public ValueTask PublishAsync(
            MarketOutlookUpdate update,
            TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels.MarketOutlookReadModel snapshot,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref count);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public void LocalUpdateContracts_AreNotActorMessages()
    {
        var updateTypes = typeof(RsiMarketOutlookUpdate).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(MarketOutlookUpdate).IsAssignableFrom(type))
            .ToArray();

        updateTypes.Should().NotBeEmpty();
        updateTypes.Should().OnlyContain(type => !typeof(IActorMessage).IsAssignableFrom(type));
        updateTypes.Should().OnlyContain(type => !typeof(IEvent).IsAssignableFrom(type));
        updateTypes.Select(type => type.GetProperty(nameof(MarketOutlookUpdate.Kind))!.GetMethod)
            .Should().OnlyContain(method => method != null);
    }

    [Fact]
    public async Task ConcurrentProducers_UseOneProcessorAndPreserveSiblingState()
    {
        await using var runtime = await MarketOutlookProcessorTestRuntime.StartAsync();
        const int updatesPerKind = 500;

        var esProducer = Task.Run(() =>
        {
            for (var index = 1; index <= updatesPerKind; index++)
                runtime.Channel.Submit(Vix(index));
        });
        var healthProducer = Task.Run(() =>
        {
            for (var index = 1; index <= updatesPerKind; index++)
                runtime.Channel.Submit(new FeedHealthMarketOutlookUpdate
                {
                    UpdateId = Guid.NewGuid(),
                    EntityId = Id,
                    ReceivedAtUtc = DateTime.UtcNow,
                    MarketDataAsOfUtc = DateTime.UtcNow,
                    Health = "Up",
                    Reason = $"sample-{index}"
                });
        });

        await Task.WhenAll(esProducer, healthProducer);
        await runtime.DrainAsync();

        runtime.Cache.TryGetCurrent(Id, out var current).Should().BeTrue();
        current.VixFuturesPrice.Should().BeGreaterThan(0);
        current.FeedHealth.Should().Be("Up");
        var metrics = runtime.Processor.GetMetrics();
        metrics.Updates[MarketOutlookUpdateKind.VixPrice].Received.Should().Be(updatesPerKind);
        metrics.Updates[MarketOutlookUpdateKind.FeedHealth].Received.Should().Be(updatesPerKind);
        metrics.Updates.Values.Sum(value => value.Applied).Should().Be(1_000);
        metrics.Updates.Values.Sum(value => value.Published).Should().Be(1_000);
    }

    [Fact]
    public async Task BoundedChannel_CoalescesLatestOverflowAndReportsIt()
    {
        var cache = new MarketOutlookHotCache();
        var metrics = new MarketOutlookProcessorMetrics();
        var channel = new MarketOutlookUpdateChannel(metrics, 1);
        var publisher = Substitute.For<IMarketOutlookSnapshotPublisher>();
        var processor = new MarketOutlookUpdateProcessor(
            channel, channel, cache, cache, publisher, metrics,
            Substitute.For<Microsoft.Extensions.Logging.ILogger<MarketOutlookUpdateProcessor>>());
        channel.Submit(Vix(1));
        channel.Submit(Vix(2));
        channel.Submit(Vix(3));

        await processor.StartAsync(CancellationToken.None);
        try
        {
            (await processor.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
            cache.TryGetCurrent(Id, out var current).Should().BeTrue();
            current.VixFuturesPrice.Should().Be(3m);
            var values = processor.GetMetrics().Updates[MarketOutlookUpdateKind.VixPrice];
            values.Received.Should().Be(3);
            values.Enqueued.Should().Be(1);
            values.Coalesced.Should().Be(2);
            values.Applied.Should().Be(2, "the bounded lane and latest overflow value are processed");
        }
        finally
        {
            await processor.StopAsync(CancellationToken.None);
            processor.Dispose();
        }
    }

    [Fact]
    public void MetricsFailure_CannotEscapeProducerSubmission()
    {
        var channel = new MarketOutlookUpdateChannel(new ThrowingOperationsRecorder(), 1);

        var action = () => channel.Submit(Vix(1));

        action.Should().NotThrow();
        channel.PendingCount.Should().Be(1);
    }

    [Fact]
    public async Task OldestPendingTime_AdvancesAsTheSingleReaderCompletesItems()
    {
        var metrics = new MarketOutlookProcessorMetrics();
        var channel = new MarketOutlookUpdateChannel(metrics, 2);
        var oldest = DateTime.UtcNow.AddSeconds(-10);
        var newest = DateTime.UtcNow.AddSeconds(-1);
        channel.Submit(Vix(1) with { ReceivedAtUtc = oldest });
        channel.Submit(Vix(2) with { ReceivedAtUtc = newest });

        await using (var reader = channel.ReadAllAsync(CancellationToken.None).GetAsyncEnumerator())
        {
            (await reader.MoveNextAsync()).Should().BeTrue();
            channel.OldestPendingUtc.Should().Be(oldest);
            (await reader.MoveNextAsync()).Should().BeTrue();
            channel.OldestPendingUtc.Should().Be(newest);
        }

        channel.PendingCount.Should().Be(0);
        channel.OldestPendingUtc.Should().BeNull();
    }

    [Fact]
    public async Task MalformedUpdate_IsMeasuredAndDoesNotStopFollowingUpdates()
    {
        await using var runtime = await MarketOutlookProcessorTestRuntime.StartAsync();
        runtime.Channel.Submit(new UnsupportedMarketOutlookUpdate
        {
            UpdateId = Guid.NewGuid(),
            EntityId = Id,
            ReceivedAtUtc = DateTime.UtcNow,
            MarketDataAsOfUtc = DateTime.UtcNow
        });
        runtime.Channel.Submit(Vix(25));

        await runtime.DrainAsync();

        runtime.Cache.TryGetCurrent(Id, out var current).Should().BeTrue();
        current.VixFuturesPrice.Should().Be(25m);
        runtime.Processor.GetMetrics().Updates[MarketOutlookUpdateKind.VixPrice].Failed
            .Should().Be(1);
    }

    [Fact]
    public async Task PublicationFailure_DoesNotRollbackCommittedSnapshot()
    {
        await using var runtime = await MarketOutlookProcessorTestRuntime.StartAsync();
        runtime.Publisher.PublishAsync(
                Arg.Any<MarketOutlookUpdate>(),
                Arg.Any<TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels.MarketOutlookReadModel>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException(new IOException("injected publication failure")));

        runtime.Channel.Submit(Vix(21));
        await runtime.DrainAsync();

        runtime.Cache.TryGetCurrent(Id, out var current).Should().BeTrue();
        current.VixFuturesPrice.Should().Be(21m);
        var values = runtime.Processor.GetMetrics().Updates[MarketOutlookUpdateKind.VixPrice];
        values.Applied.Should().Be(1);
        values.Published.Should().Be(0);
        values.Failed.Should().Be(1);
        runtime.Cache.GetMetrics().NotificationFailures.Should().Be(1);
    }

    [Fact]
    public async Task Recompose_RepublishesCurrentInputsWithoutFabricatingMarketTime()
    {
        await using var runtime = await MarketOutlookProcessorTestRuntime.StartAsync();
        var sourceTime = DateTime.UtcNow.AddMinutes(-2);
        runtime.Channel.Submit(Vix(20, sourceTime));
        await runtime.DrainAsync();
        runtime.Cache.TryGetCurrent(Id, out var before).Should().BeTrue();

        runtime.Processor.RequestRecompose(Id).Should().BeTrue();
        await runtime.DrainAsync();

        runtime.Cache.TryGetCurrent(Id, out var after).Should().BeTrue();
        after.VixFuturesPrice.Should().Be(before.VixFuturesPrice);
        after.MarketDataAsOfUtc.Should().Be(before.MarketDataAsOfUtc);
        after.UpdatedAtUtc.Should().BeOnOrAfter(before.UpdatedAtUtc);
        runtime.Processor.GetMetrics().Updates[MarketOutlookUpdateKind.Recompose].Published
            .Should().Be(1);
        runtime.Processor.GetMetrics().Updates[MarketOutlookUpdateKind.Recompose].Changed
            .Should().Be(0, "republishing current inputs does not claim a source-value change");
    }

    [Fact]
    public async Task StartupHydration_ReplacesPersistedComponents_ThenLiveUpdatesReplaceBaseline()
    {
        await using var runtime = await MarketOutlookProcessorTestRuntime.StartAsync();
        runtime.Channel.Submit(Vix(31m));
        await runtime.DrainAsync();

        runtime.Channel.Submit(new HydrateMarketOutlookUpdate
        {
            UpdateId = Guid.NewGuid(),
            EntityId = Id,
            ReceivedAtUtc = DateTime.UtcNow,
            MarketDataAsOfUtc = DateTime.UtcNow.AddMinutes(-5),
            Baseline = new MarketOutlookInputState
            {
                EntityId = Id,
                VixFuturesSessionOpenPrice = 20m,
                VixFuturesPrice = 18m,
                FuturesTradeSignal = new FuturesTradeSignalV2ReadModel
                {
                    ContractId = Id.ContractId,
                    ValueDate = Id.ValueDate,
                    FuturesPrice = 6_100d
                },
                CurrentEsPrice = 6_100m,
                MarketDataAsOfUtc = DateTime.UtcNow.AddMinutes(-5)
            }
        });
        await runtime.DrainAsync();

        runtime.Cache.TryGetCurrent(Id, out var current).Should().BeTrue();
        current.VixFuturesPrice.Should().Be(18m, "startup persistence establishes the baseline");
        current.FuturesEodData.PriceVolatility.Should().Be(PriceVolatilityType.Falling);
        current.FuturesTradeSignal.Should().NotBeNull("the stored value is restored");
        current.RefreshTrigger.Should().Be(MarketOutlookRefreshTrigger.PersistedBaseline);

        runtime.Channel.Submit(Vix(32m));
        await runtime.DrainAsync();

        runtime.Cache.TryGetCurrent(Id, out current).Should().BeTrue();
        current.VixFuturesPrice.Should().Be(32m, "live updates replace the startup baseline");
        current.FuturesEodData.PriceVolatility.Should().Be(PriceVolatilityType.Rising);

        runtime.Channel.Submit(new RsiMarketOutlookUpdate
        {
            UpdateId = Guid.NewGuid(),
            EntityId = Id,
            ReceivedAtUtc = DateTime.UtcNow,
            MarketDataAsOfUtc = DateTime.UtcNow,
            Signal = new FuturesRsiSignalReadModel { RSI = 55d, IsWarm = true }
        });
        await runtime.DrainAsync();

        runtime.Cache.TryGetCurrent(Id, out current).Should().BeTrue();
        current.FuturesEodData.PriceVolatility.Should().Be(
            PriceVolatilityType.Rising,
            "unrelated component refreshes preserve the VX-derived classification");
    }

    [Fact]
    public async Task Metrics_IsolateInactiveKindsAndRetainMarketDataTime()
    {
        await using var runtime = await MarketOutlookProcessorTestRuntime.StartAsync();
        var marketTime = DateTime.UtcNow.AddSeconds(-15);
        runtime.Channel.Submit(Vix(19, marketTime));

        await runtime.DrainAsync();

        var snapshot = runtime.Processor.GetMetrics();
        snapshot.Updates[MarketOutlookUpdateKind.VixPrice].Applied.Should().Be(1);
        snapshot.Updates[MarketOutlookUpdateKind.VixPrice].LastMarketDataAsOfUtc
            .Should().Be(marketTime);
        snapshot.Updates[MarketOutlookUpdateKind.Tdi].Received.Should().Be(0);
    }

    [Fact]
    public async Task Stop_DrainsAcceptedUpdatesAndClearsReadiness()
    {
        var cache = new MarketOutlookHotCache();
        var metrics = new MarketOutlookProcessorMetrics();
        var channel = new MarketOutlookUpdateChannel(metrics);
        var publisher = Substitute.For<IMarketOutlookSnapshotPublisher>();
        using var processor = new MarketOutlookUpdateProcessor(
            channel, channel, cache, cache, publisher, metrics,
            Substitute.For<Microsoft.Extensions.Logging.ILogger<MarketOutlookUpdateProcessor>>());
        await processor.StartAsync(CancellationToken.None);
        processor.IsReady.Should().BeTrue();
        for (var index = 1; index <= 100; index++)
            channel.Submit(Vix(index));

        await processor.StopAsync(CancellationToken.None);

        var snapshot = processor.GetMetrics();
        snapshot.PendingCount.Should().Be(0);
        snapshot.Updates[MarketOutlookUpdateKind.VixPrice].Published.Should().Be(100);
        snapshot.IsProcessorReady.Should().BeFalse();
    }

    [Fact]
    public async Task SustainedBurst_ReconcilesTenThousandUpdatesWithinBoundedResourceBudget()
    {
        var cache = new MarketOutlookHotCache();
        var metrics = new MarketOutlookProcessorMetrics();
        var channel = new MarketOutlookUpdateChannel(metrics, 12_000);
        var publisher = new CountingPublisher();
        using var processor = new MarketOutlookUpdateProcessor(
            channel, channel, cache, cache, publisher, metrics,
            Substitute.For<Microsoft.Extensions.Logging.ILogger<MarketOutlookUpdateProcessor>>());
        await processor.StartAsync(CancellationToken.None);
        var allocatedBefore = GC.GetTotalAllocatedBytes(true);
        var stopwatch = Stopwatch.StartNew();

        for (var index = 1; index <= 10_000; index++)
            channel.Submit(Vix(index));
        (await processor.WaitForIdleAsync(TimeSpan.FromSeconds(15))).Should().BeTrue();

        stopwatch.Stop();
        var allocatedBytes = GC.GetTotalAllocatedBytes(false) - allocatedBefore;
        var snapshot = processor.GetMetrics();
        snapshot.Updates[MarketOutlookUpdateKind.VixPrice].Received.Should().Be(10_000);
        snapshot.Updates[MarketOutlookUpdateKind.VixPrice].Applied.Should().Be(10_000);
        snapshot.Updates[MarketOutlookUpdateKind.VixPrice].Published.Should().Be(10_000);
        publisher.Count.Should().Be(10_000);
        allocatedBytes.Should().BeLessThan(512L * 1024 * 1024);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15));
        output.WriteLine(
            "10,000 updates: {0:N0} updates/s, {1:N0} allocated bytes, {2:N1} ms",
            10_000 / stopwatch.Elapsed.TotalSeconds,
            allocatedBytes,
            stopwatch.Elapsed.TotalMilliseconds);

        await processor.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void CacheWriterCapability_IsConsumedOnlyByProcessorInProductionAssembly()
    {
        var consumers = typeof(MarketOutlookUpdateProcessor).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => type.GetConstructors().SelectMany(ctor => ctor.GetParameters())
                .Any(parameter => parameter.ParameterType == typeof(IMarketOutlookHotCacheWriter)))
            .ToArray();

        consumers.Should().Equal(typeof(MarketOutlookUpdateProcessor));
    }

    static VixPriceMarketOutlookUpdate Vix(
        decimal price,
        DateTime? marketTime = null)
    {
        var now = marketTime ?? DateTime.UtcNow;
        return new()
        {
            UpdateId = Guid.NewGuid(),
            EntityId = Id,
            ReceivedAtUtc = DateTime.UtcNow,
            MarketDataAsOfUtc = now,
            Price = price,
            EventSource = "unit-test"
        };
    }
}
