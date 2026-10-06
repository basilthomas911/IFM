using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Projector;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.Realtime;

public sealed class FuturesEodMinuteWriterTests
{
    [Fact]
    public async Task StorageFallback_IsNotCached_AndConcurrentLiveSnapshotWins()
    {
        var cache = new CurrentFuturesEodCache();
        var row = Row();
        var changed = row with { ClosePrice = 102m };
        (await cache.ReadAsync(row.ContractId, row.ValueDate, () => Task.FromResult<FuturesEodDataV2ReadModel?>(row))).Should().Be(row);
        cache.TryGet(row.ContractId, row.ValueDate, out _).Should().BeFalse();
        var loaded = await cache.ReadAsync(row.ContractId, row.ValueDate, () =>
        {
            cache.Publish(changed);
            return Task.FromResult<FuturesEodDataV2ReadModel?>(row);
        });
        loaded.Should().Be(changed);
        var fallbackCalls = 0;
        loaded = await cache.ReadAsync(row.ContractId, row.ValueDate, () =>
        {
            fallbackCalls++;
            return Task.FromResult<FuturesEodDataV2ReadModel?>(row);
        });
        fallbackCalls.Should().Be(0);
        loaded.Should().Be(changed);
    }

    [Fact]
    public async Task ConcurrentLiveSnapshot_WinsEvenWhenStorageFallbackFails()
    {
        var cache = new CurrentFuturesEodCache();
        var row = Row();
        var result = await cache.ReadAsync(row.ContractId, row.ValueDate, () =>
        {
            cache.Publish(row);
            return Task.FromException<FuturesEodDataV2ReadModel?>(new IOException("database unavailable"));
        });
        result.Should().Be(row);
        cache.TryGetStatus(row.ContractId, row.ValueDate, out var observed, out var persisted).Should().BeTrue();
        observed.Kind.Should().Be(DateTimeKind.Utc);
        persisted.Should().BeFalse();
    }

    [Fact]
    public void OlderPersistenceAcknowledgement_DoesNotReplaceLiveSnapshot_OrOtherDates()
    {
        var cache = new CurrentFuturesEodCache();
        var row = Row();
        var version = cache.Publish(row);
        var changed = row with { ClosePrice = 102m };
        cache.Publish(changed);
        cache.MarkPersisted(row.ContractId, row.ValueDate, version);
        cache.TryGet(row.ContractId, row.ValueDate, out var current).Should().BeTrue();
        current.Should().Be(changed);
        cache.TryGet(row.ContractId, row.ValueDate.AddDays(1), out _).Should().BeFalse();
    }

    [Fact]
    public async Task Drain_PreservesAllHistoryRows_AndCompletesOnlyAfterStorage_AndCanRestart()
    {
        var (projector, context, storage) = Create();
        var steps = new List<string>();
        storage.PersistRealtimeFuturesEodBatchAsync(Arg.Any<IReadOnlyList<BufferedFuturesEodRow>>())
            .Returns(call =>
            {
                var batch = call.Arg<IReadOnlyList<BufferedFuturesEodRow>>();
                batch.Should().HaveCount(2);
                batch.Should().OnlyContain(row => row.AppendHistory);
                batch.Select(row => row.Snapshot.ClosePrice).Should().Equal(100m, 101m);
                steps.Add("persisted");
                return Task.CompletedTask;
            });
        context.When(c => c.SendAsync<FuturesEodDataInsertedCompleteEvent, FuturesEodDataId>(Arg.Any<FuturesEodDataInsertedCompleteEvent>()))
            .Do(_ => steps.Add("complete"));
        await projector.StartAsync(context);
        var row = Row();
        await projector.ProcessRealtimeEventAsync(Event(row));
        await projector.ProcessRealtimeEventAsync(Event(row with { ClosePrice = 101m }));
        await storage.DidNotReceive().PersistRealtimeFuturesEodBatchAsync(Arg.Any<IReadOnlyList<BufferedFuturesEodRow>>());
        CurrentFuturesEodCache.Shared.TryGet(row.ContractId, row.ValueDate, out var live).Should().BeTrue();
        live!.ClosePrice.Should().Be(101m);
        await projector.StopAsync();
        steps.Should().Equal("persisted", "complete", "complete");
        await projector.StartAsync(context);
        await projector.StopAsync();
        var afterStop = async () => await projector.ProcessRealtimeEventAsync(Event(Row()));
        await afterStop.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task StorageFailure_RetainsSameRows_AndDoesNotPublishEarlyCompletion()
    {
        var (projector, context, storage) = Create();
        var calls = 0;
        BufferedFuturesEodRow? first = null;
        storage.PrepareRealtimeFuturesEodBatchAsync(Arg.Any<IReadOnlyList<BufferedFuturesEodRow>>())
            .Returns(call => { foreach (var row in call.Arg<IReadOnlyList<BufferedFuturesEodRow>>()) row.HistorySequenceId ??= 42; return Task.CompletedTask; });
        storage.PersistRealtimeFuturesEodBatchAsync(Arg.Any<IReadOnlyList<BufferedFuturesEodRow>>())
            .Returns(call =>
            {
                var row = call.Arg<IReadOnlyList<BufferedFuturesEodRow>>().Single();
                if (++calls == 1)
                {
                    first = row;
                    row.HistorySequenceId = 42;
                    return Task.FromException(new TimeoutException("test storage failure"));
                }
                row.Snapshot.Should().Be(first!.Snapshot);
                row.HistorySequenceId.Should().Be(42);
                return Task.CompletedTask;
            });
        context.When(c => c.SendAsync<FuturesEodDataInsertedCompleteEvent, FuturesEodDataId>(Arg.Any<FuturesEodDataInsertedCompleteEvent>()))
            .Do(_ => calls.Should().Be(2));
        await projector.StartAsync(context);
        await projector.ProcessRealtimeEventAsync(Event(Row()));
        await projector.StopAsync();
        calls.Should().Be(1);
        await context.DidNotReceive().SendAsync<FuturesEodDataInsertedCompleteEvent, FuturesEodDataId>(Arg.Any<FuturesEodDataInsertedCompleteEvent>());
        await projector.StartAsync(context);
        await projector.StopAsync();
        calls.Should().Be(2);
        await context.Received(1).SendAsync<FuturesEodDataInsertedCompleteEvent, FuturesEodDataId>(Arg.Any<FuturesEodDataInsertedCompleteEvent>());
    }

    [Fact]
    public async Task BlockedDatabase_DoesNotBlockNewLiveObservations()
    {
        var (projector, context, storage) = Create();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        storage.PersistRealtimeFuturesEodBatchAsync(Arg.Any<IReadOnlyList<BufferedFuturesEodRow>>())
            .Returns(_ => { entered.TrySetResult(); return release.Task; });
        await projector.StartAsync(context);
        var row = Row();
        try
        {
            for (var index = 0; index < 512; index++)
                await projector.ProcessRealtimeEventAsync(Event(row with { ClosePrice = 100m + index }));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await projector.ProcessRealtimeEventAsync(Event(row with { ClosePrice = 900m })).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
            CurrentFuturesEodCache.Shared.TryGet(row.ContractId, row.ValueDate, out var latest).Should().BeTrue();
            latest!.ClosePrice.Should().Be(900m);
            await context.DidNotReceive().SendAsync<FuturesEodDataInsertedCompleteEvent, FuturesEodDataId>(Arg.Any<FuturesEodDataInsertedCompleteEvent>());
        }
        finally { release.TrySetResult(); await projector.StopAsync(); }
    }

    [Fact]
    public async Task FailedCompletionNotification_DoesNotRetainSuccessfullySavedHistory()
    {
        var (projector, context, storage) = Create();
        context.When(c => c.SendAsync<FuturesEodDataInsertedCompleteEvent, FuturesEodDataId>(Arg.Any<FuturesEodDataInsertedCompleteEvent>()))
            .Do(_ => throw new IOException("notification unavailable"));
        await projector.StartAsync(context);
        await projector.ProcessRealtimeEventAsync(Event(Row()));
        await projector.StopAsync();
        await projector.StartAsync(context);
        await projector.StopAsync();
        await storage.Received(1).PersistRealtimeFuturesEodBatchAsync(Arg.Any<IReadOnlyList<BufferedFuturesEodRow>>());
    }

    [Fact]
    public async Task ColdVxLivePath_DoesNotReadDatabase_AndAccumulatesVolumeOnce()
    {
        var (projector, context, storage) = Create();
        await projector.StartAsync(context);
        var row = Row();
        var id = new FuturesEodDataId(row.ContractId, row.ValueDate);
        var tick = new TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels.FuturesTickDataV2ReadModel
        { ContractId = row.ContractId, ValueDate = row.ValueDate, Price = 20m, Size = 5 };
        var source = new VixFuturesEodDataInsertedEvent
        {
            Subject = new(ActorType.Realtime, FuturesEodDataRealtimeActor.ActorName, VixFuturesEodDataInsertedEvent.Verb, id.Format()),
            Id = Guid.NewGuid(), EntityId = id, CommandId = Guid.NewGuid(), VixFuturesTickData = tick
        };
        await projector.ProcessRealtimeEventAsync(source);
        await projector.ProcessRealtimeEventAsync(source with { Id = Guid.NewGuid(), VixFuturesTickData = tick with { Price = 21m, Size = 3 } });
        storage.ReceivedCalls().Should().BeEmpty();
        CurrentVixEodCache.Shared.TryGet(row.ContractId, row.ValueDate, out var current).Should().BeTrue();
        current!.Volume.Should().Be(8);
        current.ClosePrice.Should().Be(21m);
        await projector.StopAsync();
    }

    [Fact]
    public async Task StatisticsOnlyCorrection_DoesNotAppendHistory()
    {
        var (projector, context, storage) = Create();
        await projector.StartAsync(context);
        var row = Row();
        var id = new FuturesEodDataId(row.ContractId, row.ValueDate);
        await projector.ProcessRealtimeEventAsync(new FuturesEodSessionStatisticsUpdatedEvent
        {
            Subject = new(ActorType.Realtime, FuturesEodDataRealtimeActor.ActorName, FuturesEodSessionStatisticsUpdatedEvent.Verb, id.Format()),
            Id = Guid.NewGuid(), EntityId = id, CommandId = Guid.NewGuid(), FuturesEodData = row
        });
        await projector.StopAsync();
        await storage.Received(1).PersistRealtimeFuturesEodBatchAsync(Arg.Is<IReadOnlyList<BufferedFuturesEodRow>>(rows => rows.Count == 1 && !rows[0].AppendHistory));
    }

    [Fact]
    public void LiveNotification_PreservesUnpersistedFlagAcrossMessagePack()
    {
        var row = Row();
        var id = new FuturesEodDataId(row.ContractId, row.ValueDate);
        var notification = new FuturesEodDataUpdatedNotifyEvent
        {
            Subject = new(ActorType.Notify, FuturesEodDataUpdatedNotifyEvent.Actor, FuturesEodDataUpdatedNotifyEvent.Verb, id.Format()),
            Id = Guid.NewGuid(), EntityId = id, CommandId = Guid.NewGuid(), FuturesEodData = row,
            IsPersisted = false
        };
        var restored = MessagePack.MessagePackSerializer.Deserialize<FuturesEodDataUpdatedNotifyEvent>(
            MessagePack.MessagePackSerializer.Serialize(notification));
        restored.IsPersisted.Should().BeFalse();
        restored.FuturesEodData.Should().Be(row);
    }

    static (FuturesEodDataRealtimeProjector, IEventActorContext, IMarketDataDbContext) Create()
    {
        var factory = Substitute.For<IDbContextFactory>();
        var storage = Substitute.For<IMarketDataDbContext>();
        factory.MarketDataDb.Returns(storage);
        var context = Substitute.For<IEventActorContext>();
        context.ActorId.Returns(new ActorMailboxId(ActorType.Realtime, FuturesEodDataRealtimeActor.ActorName));
        var config = Substitute.For<IConfiguration>();
        config["MarketData:FuturesEodMinuteBatchWriter"].Returns("true");
        config["MarketData:FuturesEodHistorySpoolPath"].Returns(Path.Combine(Path.GetTempPath(), "ifm-eod-tests", Guid.NewGuid().ToString("N")));
        return (new(factory, NullLogger<FuturesEodDataRealtimeProjector>.Instance, config), context, storage);
    }

    static FuturesEodDataV2ReadModel Row() => new()
    {
        ContractId = "EOD-TEST-" + Guid.NewGuid().ToString("N"), ValueDate = new(2026, 10, 5),
        Symbol = "ES", OpenPrice = 100m, HighPrice = 110m, LowPrice = 90m, ClosePrice = 100m
    };

    static FuturesEodDataInsertedEvent Event(FuturesEodDataV2ReadModel row)
    {
        var id = new FuturesEodDataId(row.ContractId, row.ValueDate);
        return new()
        {
            Subject = new(ActorType.Realtime, FuturesEodDataRealtimeActor.ActorName, FuturesEodDataInsertedEvent.Verb, id.Format()),
            Id = Guid.NewGuid(), EntityId = id, CommandId = Guid.NewGuid(), FuturesEodData = row
        };
    }
}
