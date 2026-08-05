using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class EventSourceActorSnapshotRangeFixture
{
    public EventSourceActorSnapshotRangeFixture()
    {
        var connectionSettings = new DbConnectionSettings()
            .Add("EventSourceDbConnection", "Host=localhost;Port=5432;Database=event-source-test-db", "System.Data.Postgres")
            .Add("EventSourceActorDbConnection", "Host=localhost;Port=5432;Database=event-source-test-db", "System.Data.Postgres");
        var repositories = new Dictionary<Type, object>();
        var resolver = new DbContextResolver(type => repositories[type]);
        DbFactory = new DbContextFactory(resolver);
        var logger = Substitute.For<ILogger<DbProvider>>();
        new EventSourceSchemaDb(connectionSettings, logger).CreateAllAsync().GetAwaiter().GetResult();

        var cache = Substitute.For<IRedisCache>();
        var cacheValues = new Dictionary<string, string>();
        cache.TryGet(Arg.Any<string>(), out Arg.Any<string>()).Returns(call =>
        {
            var found = cacheValues.TryGetValue(call.ArgAt<string>(0), out var value);
            call[1] = value!;
            return found;
        });
        cache.When(instance => instance.Set(Arg.Any<string>(), Arg.Any<string>()))
            .Do(call => cacheValues[call.ArgAt<string>(0)] = call.ArgAt<string>(1));
        var blackboard = new BlackboardService(cache, new SystemTextJsonSerializer());

        EventDb = new EventSourceDbContext(connectionSettings, DbFactory, blackboard, logger);
        ActorEventDb = new EventSourceActorDbContext(connectionSettings, DbFactory, blackboard, logger);
        repositories.Add(typeof(IObjectRepository<EventSourceDbContext>), EventDb);
        repositories.Add(typeof(IObjectRepository<EventSourceActorDbContext>), ActorEventDb);
    }

    public DbContextFactory DbFactory { get; }
    public EventSourceDbContext EventDb { get; }
    public EventSourceActorDbContext ActorEventDb { get; }
}

public class EventSourceActorSnapshotRangeTests(EventSourceActorSnapshotRangeFixture fixture)
    : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
    [Fact]
    public async Task ReturnsLatestSnapshotAndLastMatchingEventsInAscendingOrder()
    {
        var stream = NewStream();
        await SaveAsync(stream,
            Snapshot(),
            RangeEvent(),
            NoiseEvent(),
            Snapshot(),
            RangeEvent(),
            NoiseEvent(),
            RangeEvent(),
            RangeEvent());

        var streamId = await fixture.ActorEventDb.GetEventStreamIdAsync(stream);
        var rawEvents = await fixture.ActorEventDb.LoadActorEventStreamAsync<TestActorState>(streamId);
        rawEvents.Should().HaveCount(8);

        var result = await LoadAsync(stream, 2);

        result.Should().HaveCount(3);
        result[0].EventTypeName.Should().Contain(nameof(FuturesRsiSignalStartedEvent));
        result.Skip(1).Should().OnlyContain(row =>
            row.EventTypeName.Contains(nameof(FuturesRsiSignalGeneratedEvent), StringComparison.Ordinal));
        result.Select(row => row.EventVersion).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task MissingSnapshotReturnsEmptyResultEvenWhenRangeEventsExist()
    {
        var stream = NewStream();
        await SaveAsync(stream, RangeEvent(), NoiseEvent(), RangeEvent());

        var result = await LoadAsync(stream, 10);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task MissingRangeEventsReturnsSnapshotOnly()
    {
        var stream = NewStream();
        await SaveAsync(stream, Snapshot(), NoiseEvent(), NoiseEvent());

        var result = await LoadAsync(stream, 10);

        result.Should().ContainSingle();
        result[0].EventTypeName.Should().Contain(nameof(FuturesRsiSignalStartedEvent));
    }

    [Fact]
    public async Task NonPositiveRangeReturnsSnapshotOnly()
    {
        var stream = NewStream();
        await SaveAsync(stream, Snapshot(), RangeEvent(), RangeEvent());

        var result = await LoadAsync(stream, -1);

        result.Should().ContainSingle();
        result[0].EventTypeName.Should().Contain(nameof(FuturesRsiSignalStartedEvent));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public async Task ExactOrLargerRangeReturnsEveryMatchingEventAfterLatestSnapshot(int lastNRange)
    {
        var stream = NewStream();
        await SaveAsync(stream,
            RangeEvent(),
            Snapshot(),
            RangeEvent(),
            NoiseEvent(),
            RangeEvent(),
            RangeEvent());

        var result = await LoadAsync(stream, lastNRange);

        result.Should().HaveCount(4);
        result[0].EventTypeName.Should().Contain(nameof(FuturesRsiSignalStartedEvent));
        result.Skip(1).Should().OnlyContain(row =>
            row.EventTypeName.Contains(nameof(FuturesRsiSignalGeneratedEvent), StringComparison.Ordinal));
        result.Select(row => row.EventVersion).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task TypedLastNRangeFiltersBeforeLimitingAndReturnsAscendingOrder()
    {
        var stream = NewStream();
        await SaveAsync(stream,
            RangeEvent(),
            NoiseEvent(),
            RangeEvent(),
            NoiseEvent(),
            RangeEvent());

        var result = await LoadTypedRangeAsync<FuturesRsiSignalGeneratedEvent>(stream, 2);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(row =>
            row.EventTypeName.Contains(nameof(FuturesRsiSignalGeneratedEvent), StringComparison.Ordinal));
        result.Select(row => row.EventVersion).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task TypedLastNRangeReturnsEmptyWhenTypeIsMissingOrRangeIsNonPositive()
    {
        var missingTypeStream = NewStream();
        await SaveAsync(missingTypeStream, NoiseEvent(), NoiseEvent());
        var nonPositiveStream = NewStream();
        await SaveAsync(nonPositiveStream, RangeEvent(), RangeEvent());

        var missingType = await LoadTypedRangeAsync<FuturesRsiSignalGeneratedEvent>(missingTypeStream, 2);
        var nonPositive = await LoadTypedRangeAsync<FuturesRsiSignalGeneratedEvent>(nonPositiveStream, 0);

        missingType.Should().BeEmpty();
        nonPositive.Should().BeEmpty();
    }

    async Task<List<EventStreamReadModel>> LoadAsync(string stream, int lastNRange)
    {
        var streamId = await fixture.ActorEventDb.GetEventStreamIdAsync(stream);
        var result = new List<EventStreamReadModel>();
        await fixture.ActorEventDb.MapReduceActorEventStreamFromSnapshotLastNRangeAsync<
            TestActorState,
            FuturesRsiSignalStartedEvent,
            FuturesRsiSignalGeneratedEvent>(streamId, lastNRange, rows => result.AddRange(rows));
        return result;
    }

    async Task<List<EventStreamReadModel>> LoadTypedRangeAsync<TEvent>(string stream, int lastNRange)
        where TEvent : IEvent
    {
        var streamId = await fixture.ActorEventDb.GetEventStreamIdAsync(stream);
        var result = new List<EventStreamReadModel>();
        await fixture.ActorEventDb.MapReduceActorEventStreamAsync<TestActorState, TEvent>(
            streamId,
            lastNRange,
            rows => result.AddRange(rows));
        return result;
    }

    async Task SaveAsync(string stream, params IEvent[] events)
        => await fixture.ActorEventDb.SaveEventsAsync(
            stream,
            Guid.NewGuid(),
            new DomainEventCollection(events));

    static string NewStream() => $"SnapshotRangeTests.{Guid.NewGuid():N}";

    static FuturesRsiSignalStartedEvent Snapshot()
        => new()
        {
            EntityId = EntityId(),
            StartedOn = DateTime.UtcNow,
            StartedBy = "integration-test"
        };

    static FuturesRsiSignalGeneratedEvent RangeEvent()
        => new()
        {
            EntityId = EntityId(),
            CreatedOn = DateTime.UtcNow,
            CreatedBy = "integration-test"
        };

    static FuturesRsiSignalStoppedEvent NoiseEvent()
        => new()
        {
            EntityId = EntityId(),
            StoppedOn = DateTime.UtcNow,
            StoppedBy = "integration-test"
        };

    static FuturesRsiSignalEntityId EntityId()
        => new("ESU6", new DateOnly(2026, 8, 5), TimeFrameType.Daily, 14);

    sealed class TestActorState : IActorState<TestActorState>
    {
        public ActorThreadId Id { get; set; }
    }
}
