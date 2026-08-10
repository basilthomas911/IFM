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
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Commands;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventProjector.ReadModels;
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

        ActorEventDb = new EventSourceActorDbContext(connectionSettings, DbFactory, blackboard, logger);
        repositories.Add(typeof(IObjectRepository<EventSourceActorDbContext>), ActorEventDb);
    }

    public DbContextFactory DbFactory { get; }
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

    [Fact]
    public async Task Command_event_lookup_distinguishes_persisted_and_unknown_commands()
    {
        var commandId = Guid.NewGuid();
        await fixture.ActorEventDb.SaveEventsAsync(
            NewStream(),
            commandId,
            new DomainEventCollection([RangeEvent()]));

        (await fixture.ActorEventDb.HasEventForCommandAsync(commandId)).Should().BeTrue();
        (await fixture.ActorEventDb.HasEventForCommandAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task Command_audit_try_insert_reports_new_then_existing_without_throwing()
    {
        var entity = new TickDataEntityId("ESU6", new DateOnly(2026, 8, 7), AssetTypeId.Futures);
        var command = new InsertFuturesTickTradeDataCommand
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesTickTradeDataCommand.Actor,
                InsertFuturesTickTradeDataCommand.Verb, entity.Format()),
            EntityId = entity
        };

        (await fixture.ActorEventDb.TryInsertCommandLogAsync(command, DateTime.UtcNow, "payload")).Should().BeTrue();
        (await fixture.ActorEventDb.TryInsertCommandLogAsync(command, DateTime.UtcNow, "payload")).Should().BeFalse();
        (await fixture.ActorEventDb.GetCommandLogAsync(command.CommandId)).Should().NotBeNull();
    }

    [Fact]
    public async Task Projector_claim_transition_and_terminalization_reject_stale_owners()
    {
        var stream = NewStream();
        var sourceEvent = RangeEvent();
        var saved = await fixture.ActorEventDb.SaveEventsAsync(
            stream,
            Guid.NewGuid(),
            new DomainEventCollection([sourceEvent]));
        var persisted = saved.Single();
        var streamId = await fixture.ActorEventDb.GetEventStreamIdAsync(stream);
        var projectorName = $"ReliabilityProjector.{Guid.NewGuid():N}";
        var nowUtc = DateTime.UtcNow;
        var initialState = NewExecutionState(persisted.EventId, streamId, projectorName, nowUtc);
        var createAttempts = Enumerable.Range(0, 8)
            .Select(_ => fixture.ActorEventDb.TryCreateEventProjectorExecutionStateAsync(initialState))
            .ToArray();
        var creates = await Task.WhenAll(createAttempts);
        creates.Where(state => state is not null).Should().ContainSingle();
        var claimAttempts = Enumerable.Range(0, 16)
            .Select(_ => fixture.ActorEventDb.TryClaimEventProjectorExecutionAsync(
                persisted.EventId,
                projectorName,
                Guid.NewGuid(),
                nowUtc,
                TimeSpan.FromMinutes(2)))
            .ToArray();
        var claims = await Task.WhenAll(claimAttempts);
        claims.Where(state => state is not null).Should().ContainSingle();
        var claimed = claims.Single(state => state is not null)!;
        claimed.Revision.Should().Be(1);

        var transition = new EventProjectorStateTransition(
            persisted.EventId,
            projectorName,
            claimed.ExecutionToken!.Value,
            claimed.Revision,
            EventProjectorStageType.ValidateSourceEvent,
            EventProjectorStageType.ApplyProjection,
            EventProjectorOutcomeType.Processing,
            EventProjectorStageType.ValidateSourceEvent);
        var transitioned = await fixture.ActorEventDb.TryTransitionEventProjectorExecutionAsync(transition, nowUtc);
        var staleTransition = await fixture.ActorEventDb.TryTransitionEventProjectorExecutionAsync(transition, nowUtc);

        transitioned.Should().NotBeNull();
        transitioned!.Revision.Should().Be(2);
        staleTransition.Should().BeNull();

        var renewed = await fixture.ActorEventDb.TryRenewEventProjectorExecutionAsync(
            persisted.EventId,
            projectorName,
            claimed.ExecutionToken.Value,
            transitioned.Revision,
            nowUtc.AddSeconds(1),
            TimeSpan.FromMinutes(2));
        renewed.Should().NotBeNull();
        renewed!.Revision.Should().Be(3);

        var terminal = await fixture.ActorEventDb.TryTerminalizeEventProjectorExecutionAsync(
            new EventProjectorStateTransition(
                persisted.EventId,
                projectorName,
                claimed.ExecutionToken.Value,
                renewed.Revision,
                EventProjectorStageType.ApplyProjection,
                EventProjectorStageType.Completed,
                EventProjectorOutcomeType.Completed,
                EventProjectorStageType.PersistCompletion),
            nowUtc.AddSeconds(2));

        terminal.Should().NotBeNull();
        terminal!.Stage.Should().Be(EventProjectorStageType.Completed);
        terminal.Outcome.Should().Be(EventProjectorOutcomeType.Completed);
        terminal.ExecutionToken.Should().BeNull();
        terminal.LeaseExpiresAtUtc.Should().BeNull();
        (await fixture.ActorEventDb.TryRenewEventProjectorExecutionAsync(
            persisted.EventId,
            projectorName,
            claimed.ExecutionToken.Value,
            renewed.Revision,
            nowUtc.AddSeconds(3),
            TimeSpan.FromMinutes(2))).Should().BeNull();
    }

    [Fact]
    public async Task Projector_expired_lease_can_be_taken_over_and_fences_the_previous_owner()
    {
        var stream = NewStream();
        var saved = await fixture.ActorEventDb.SaveEventsAsync(
            stream,
            Guid.NewGuid(),
            new DomainEventCollection([RangeEvent()]));
        var persisted = saved.Single();
        var streamId = await fixture.ActorEventDb.GetEventStreamIdAsync(stream);
        var projectorName = $"LeaseTakeoverProjector.{Guid.NewGuid():N}";
        var nowUtc = DateTime.UtcNow;
        (await fixture.ActorEventDb.TryCreateEventProjectorExecutionStateAsync(
            NewExecutionState(persisted.EventId, streamId, projectorName, nowUtc))).Should().NotBeNull();

        var firstToken = Guid.NewGuid();
        var firstOwner = await fixture.ActorEventDb.TryClaimEventProjectorExecutionAsync(
            persisted.EventId,
            projectorName,
            firstToken,
            nowUtc,
            TimeSpan.FromSeconds(10));
        var secondToken = Guid.NewGuid();
        var secondOwner = await fixture.ActorEventDb.TryClaimEventProjectorExecutionAsync(
            persisted.EventId,
            projectorName,
            secondToken,
            nowUtc.AddSeconds(11),
            TimeSpan.FromMinutes(2));

        firstOwner.Should().NotBeNull();
        secondOwner.Should().NotBeNull();
        secondOwner!.ExecutionToken.Should().Be(secondToken);
        secondOwner.Revision.Should().Be(firstOwner!.Revision + 1);

        var staleOwnerTransition = await fixture.ActorEventDb.TryTransitionEventProjectorExecutionAsync(
            new EventProjectorStateTransition(
                persisted.EventId,
                projectorName,
                firstToken,
                firstOwner.Revision,
                EventProjectorStageType.ValidateSourceEvent,
                EventProjectorStageType.ApplyProjection,
                EventProjectorOutcomeType.Processing,
                EventProjectorStageType.ValidateSourceEvent),
            nowUtc.AddSeconds(11));
        staleOwnerTransition.Should().BeNull();
    }

    [Fact]
    public async Task Projector_recovery_page_uses_bounded_event_id_keyset_without_duplicates()
    {
        var stream = NewStream();
        var sourceEvents = new IEvent[] { RangeEvent(), RangeEvent(), RangeEvent() };
        var saved = await fixture.ActorEventDb.SaveEventsAsync(
            stream,
            Guid.NewGuid(),
            new DomainEventCollection(sourceEvents));
        var streamId = await fixture.ActorEventDb.GetEventStreamIdAsync(stream);
        var projectorName = $"RecoveryProjector.{Guid.NewGuid():N}";
        var nowUtc = DateTime.UtcNow;
        foreach (var sourceEvent in saved)
        {
            (await fixture.ActorEventDb.TryCreateEventProjectorExecutionStateAsync(
                NewExecutionState(sourceEvent.EventId, streamId, projectorName, nowUtc))).Should().NotBeNull();
        }

        var firstPage = await fixture.ActorEventDb.GetEventProjectorRecoveryPageAsync(
            projectorName,
            [nameof(FuturesRsiSignalGeneratedEvent)],
            0,
            nowUtc,
            2);
        var secondPage = await fixture.ActorEventDb.GetEventProjectorRecoveryPageAsync(
            projectorName,
            [nameof(FuturesRsiSignalGeneratedEvent)],
            firstPage[^1].State.EventId,
            nowUtc,
            2);

        firstPage.Should().HaveCount(2);
        secondPage.Should().ContainSingle();
        firstPage.Concat(secondPage).Select(item => item.State.EventId)
            .Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        firstPage.Concat(secondPage).Should().OnlyContain(item =>
            item.EventLog.EventStreamId == item.State.EventStreamId
            && item.EventLog.EventName == item.State.SourceEventName);
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

    static EventProjectorExecutionStateReadModel NewExecutionState(
        long eventId,
        long eventStreamId,
        string projectorName,
        DateTime nowUtc)
        => new(
            EventId: eventId,
            ActorName: "ReliabilityTestActor",
            ProjectorName: projectorName,
            IsReplay: true,
            AttemptNumber: 0,
            Outcome: EventProjectorOutcomeType.Processing,
            Stage: EventProjectorStageType.ValidateSourceEvent,
            ErrorMessage: string.Empty,
            CreatedTimestamp: nowUtc,
            UpdatedTimestamp: nowUtc,
            EventStreamId: eventStreamId,
            SourceEventName: nameof(FuturesRsiSignalGeneratedEvent),
            Revision: 0,
            ExecutionToken: null,
            LeaseExpiresAtUtc: null,
            RetryCount: 0,
            NextAttemptAtUtc: null,
            LastErrorAtUtc: null,
            BlockedReason: string.Empty,
            LastCompletedStage: EventProjectorStageType.None,
            UpdatedAtUtc: nowUtc);

    sealed class TestActorState : IActorState<TestActorState>
    {
        public ActorThreadId Id { get; set; }
    }
}
