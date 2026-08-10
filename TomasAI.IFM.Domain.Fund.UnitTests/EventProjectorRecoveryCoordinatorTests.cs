using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventProjector.ReadModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.Fund.UnitTests;

public sealed class EventProjectorRecoveryCoordinatorTests
{
    [Fact]
    public async Task Empty_inventory_completes_without_claiming_or_enqueueing()
    {
        var db = Substitute.For<IEventSourceActorDbContext>();
        db.GetEventProjectorRecoveryPageAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<long>(),
                Arg.Any<DateTime>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Array.Empty<EventProjectorRecoveryItemReadModel>());
        var queue = Substitute.For<IDurableReplayQueue>();
        var coordinator = CreateCoordinator(db, queue, batchSize: 3, concurrency: 2);

        var result = await coordinator.RecoverAsync(
            "FundCommandActor",
            "FundEventProjector",
            [typeof(FundCreatedEvent)]);

        result.Should().Be(new EventProjectorRecoveryResult(0, 0, 0, 0));
        await db.DidNotReceive().TryClaimEventProjectorExecutionAsync(
            Arg.Any<long>(),
            Arg.Any<string>(),
            Arg.Any<Guid>(),
            Arg.Any<DateTime>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
        await queue.DidNotReceive().EnqueueAsync(
            Arg.Any<string>(),
            Arg.Any<IEvent>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 2)]
    public async Task Small_and_partial_final_pages_publish_every_claimed_event(
        int eventCount,
        int expectedPageQueries)
    {
        const int batchSize = 3;
        var items = Enumerable.Range(1, eventCount)
            .Select(index => Item(index, index % 2 + 1))
            .ToArray();
        var db = CreatePagedDatabase(items, batchSize);
        var queue = Substitute.For<IDurableReplayQueue>();
        var coordinator = CreateCoordinator(db, queue, batchSize, concurrency: 2);

        var result = await coordinator.RecoverAsync(
            "FundCommandActor",
            "FundEventProjector",
            [typeof(FundCreatedEvent)]);

        result.Should().Be(new EventProjectorRecoveryResult(eventCount, eventCount, 0, 0));
        await queue.Received(eventCount).EnqueueAsync(
            "FundEventProjector",
            Arg.Any<IEvent>(),
            Arg.Any<CancellationToken>());
        await db.Received(expectedPageQueries).GetEventProjectorRecoveryPageAsync(
            "FundEventProjector",
            Arg.Any<IReadOnlyCollection<string>>(),
            Arg.Any<long>(),
            Arg.Any<DateTime>(),
            batchSize,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Multi_page_recovery_is_sequential_per_stream_and_bounded_across_streams()
    {
        var items = new[]
        {
            Item(1, 11), Item(2, 22), Item(3, 11),
            Item(4, 22), Item(5, 11), Item(6, 22)
        };
        var db = CreatePagedDatabase(items, batchSize: 3);
        var queue = Substitute.For<IDurableReplayQueue>();
        var activeByStream = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var maxByStream = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var published = new ConcurrentQueue<(string Stream, long EventId)>();
        queue.EnqueueAsync(
                Arg.Any<string>(),
                Arg.Any<IEvent>(),
                Arg.Any<CancellationToken>())
            .Returns(call => RecordAsync(call.ArgAt<IEvent>(1), call.ArgAt<CancellationToken>(2)));
        var coordinator = CreateCoordinator(db, queue, batchSize: 3, concurrency: 2);

        var result = await coordinator.RecoverAsync(
            "FundCommandActor",
            "FundEventProjector",
            [typeof(FundCreatedEvent)]);

        result.Should().Be(new EventProjectorRecoveryResult(6, 6, 0, 0));
        published.Where(item => item.Stream == "stream-11").Select(item => item.EventId).Should().Equal(1, 3, 5);
        published.Where(item => item.Stream == "stream-22").Select(item => item.EventId).Should().Equal(2, 4, 6);
        maxByStream.Values.Should().OnlyContain(value => value == 1);
        await db.Received(3).GetEventProjectorRecoveryPageAsync(
            "FundEventProjector",
            Arg.Any<IReadOnlyCollection<string>>(),
            Arg.Any<long>(),
            Arg.Any<DateTime>(),
            3,
            Arg.Any<CancellationToken>());

        async ValueTask RecordAsync(IEvent domainEvent, CancellationToken cancellationToken)
        {
            var stream = domainEvent.AggregateId;
            var active = activeByStream.AddOrUpdate(stream, 1, static (_, current) => current + 1);
            maxByStream.AddOrUpdate(stream, active, (_, current) => Math.Max(current, active));
            await Task.Delay(5, cancellationToken);
            published.Enqueue((stream, domainEvent.EventId));
            activeByStream.AddOrUpdate(stream, 0, static (_, current) => current - 1);
        }
    }

    [Fact]
    public async Task Concurrent_recovery_instances_enqueue_each_event_once_after_claim_contention()
    {
        var items = new[] { Item(10, 11), Item(11, 22), Item(12, 11), Item(13, 22) };
        var db = CreatePagedDatabase(items, batchSize: 4, enforceSingleClaim: true);
        var queue = Substitute.For<IDurableReplayQueue>();
        var enqueued = 0;
        queue.EnqueueAsync(Arg.Any<string>(), Arg.Any<IEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref enqueued);
                return ValueTask.CompletedTask;
            });
        var first = CreateCoordinator(db, queue, batchSize: 4, concurrency: 2);
        var second = CreateCoordinator(db, queue, batchSize: 4, concurrency: 2);

        var results = await Task.WhenAll(
            first.RecoverAsync("FundCommandActor", "FundEventProjector", [typeof(FundCreatedEvent)]),
            second.RecoverAsync("FundCommandActor", "FundEventProjector", [typeof(FundCreatedEvent)]));

        enqueued.Should().Be(4);
        results.Sum(result => result.Queued).Should().Be(4);
        results.Sum(result => result.ClaimConflicts).Should().Be(4);
    }

    [Fact]
    public async Task Cancellation_after_claim_stops_recovery_and_leaves_later_events_unpublished()
    {
        var items = new[] { Item(20, 11), Item(21, 11), Item(22, 11) };
        var db = CreatePagedDatabase(items, batchSize: 3);
        var queue = Substitute.For<IDurableReplayQueue>();
        using var cancellation = new CancellationTokenSource();
        var enqueued = 0;
        queue.EnqueueAsync(Arg.Any<string>(), Arg.Any<IEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref enqueued) == 1)
                    cancellation.Cancel();
                return ValueTask.CompletedTask;
            });
        var coordinator = CreateCoordinator(db, queue, batchSize: 3, concurrency: 1);

        var recover = () => coordinator.RecoverAsync(
            "FundCommandActor",
            "FundEventProjector",
            [typeof(FundCreatedEvent)],
            cancellation.Token);

        await recover.Should().ThrowAsync<OperationCanceledException>();
        enqueued.Should().Be(1);
    }

    static EventProjectorRecoveryCoordinator CreateCoordinator(
        IEventSourceActorDbContext db,
        IDurableReplayQueue queue,
        int batchSize,
        int concurrency)
        => new(
            db,
            queue,
            CreateBlackboard(),
            new EventProjectorReliabilityOptions
            {
                BoundedRecoveryEnabled = true,
                RecoveryBatchSize = batchSize,
                RecoveryStreamConcurrency = concurrency
            },
            Substitute.For<ILogger>());

    static IBlackboardService CreateBlackboard()
    {
        var redis = Substitute.For<IRedisCache>();
        redis.Get(Arg.Any<string>()).Returns(string.Empty);
        return new BlackboardService(redis, new SystemTextJsonSerializer());
    }

    static IEventSourceActorDbContext CreatePagedDatabase(
        IReadOnlyCollection<EventProjectorRecoveryItemReadModel> items,
        int batchSize,
        bool enforceSingleClaim = false)
    {
        var db = Substitute.For<IEventSourceActorDbContext>();
        var byEventId = items.ToDictionary(item => item.State.EventId);
        var claims = new ConcurrentDictionary<long, byte>();
        db.GetEventProjectorRecoveryPageAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<long>(),
                Arg.Any<DateTime>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(call => items
                .Where(item => item.State.EventId > call.ArgAt<long>(2))
                .OrderBy(item => item.State.EventId)
                .Take(batchSize)
                .ToArray());
        db.TryClaimEventProjectorExecutionAsync(
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<DateTime>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var eventId = call.ArgAt<long>(0);
                if (enforceSingleClaim && !claims.TryAdd(eventId, 0))
                    return Task.FromResult<EventProjectorExecutionStateReadModel?>(null);
                var token = call.ArgAt<Guid>(2);
                var nowUtc = call.ArgAt<DateTime>(3);
                var lease = call.ArgAt<TimeSpan>(4);
                return Task.FromResult<EventProjectorExecutionStateReadModel?>(byEventId[eventId].State with
                {
                    Revision = byEventId[eventId].State.Revision + 1,
                    ExecutionToken = token,
                    LeaseExpiresAtUtc = nowUtc.Add(lease)
                });
            });
        return db;
    }

    static EventProjectorRecoveryItemReadModel Item(long eventId, long streamId)
    {
        var nowUtc = new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);
        var sourceEvent = new FundCreatedEvent
        {
            NewFund = SampleData.Fund,
            AggregateId = $"stream-{streamId}"
        };
        return new EventProjectorRecoveryItemReadModel(
            new EventLogReadModel(
                streamId,
                nameof(FundCreatedEvent),
                typeof(FundCreatedEvent).AssemblyQualifiedName!,
                eventId,
                Newtonsoft.Json.JsonConvert.SerializeObject(sourceEvent),
                Guid.NewGuid(),
                $"{nowUtc:o}"),
            new EventProjectorExecutionStateReadModel(
                eventId,
                "FundCommandActor",
                "FundEventProjector",
                true,
                0,
                EventProjectorOutcomeType.Processing,
                EventProjectorStageType.PublishProcessingEvent,
                string.Empty,
                nowUtc,
                nowUtc,
                streamId,
                nameof(FundCreatedEvent),
                0,
                null,
                null,
                0,
                null,
                null,
                string.Empty,
                EventProjectorStageType.None,
                nowUtc));
    }
}
