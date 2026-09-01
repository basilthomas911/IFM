using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class EventSourceActorSnapshotRangeFixture
{
    readonly IDbConnectionSettings _connectionSettings;
    readonly IBlackboardService _blackboard;
    readonly ILogger<DbProvider> _logger;

    public EventSourceActorSnapshotRangeFixture()
    {
        _connectionSettings = new DbConnectionSettings()
            .Add("EventSourceActorDbConnection", "Host=localhost;Port=5432;Database=event-source-test-db", "System.Data.Postgres");
        var repositories = new Dictionary<Type, object>();
        var resolver = new DbContextResolver(type => repositories[type]);
        DbFactory = new DbContextFactory(resolver);
        _logger = Substitute.For<ILogger<DbProvider>>();
        new EventSourceSchemaDb(_connectionSettings, _logger).CreateAllAsync().GetAwaiter().GetResult();

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
        _blackboard = new BlackboardService(cache, new SystemTextJsonSerializer());

        ActorEventDb = CreateActorEventDb();
        repositories.Add(typeof(IObjectRepository<EventSourceActorDbContext>), ActorEventDb);
    }

    public DbContextFactory DbFactory { get; }
    public EventSourceActorDbContext ActorEventDb { get; }

    public EventSourceActorDbContext CreateActorEventDb()
        => new(_connectionSettings, DbFactory, _blackboard, _logger);
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
    public async Task TypedLastNRangePreservesRsiHistoryAcrossNewerRestartMarker()
    {
        var stream = NewStream();
        await SaveAsync(stream,
            RangeEvent(),
            RangeEvent(),
            Snapshot(),
            RangeEvent());

        var result = await LoadTypedRangeAsync<FuturesRsiSignalGeneratedEvent>(stream, 3);

        result.Should().HaveCount(3);
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
    public async Task Legacy_parse_audit_hands_its_new_reservation_to_the_central_logger_once()
    {
        var entity = new TickDataEntityId("ESU6", new DateOnly(2026, 8, 7), AssetTypeId.Futures);
        var command = new InsertFuturesTickTradeDataCommand
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesTickTradeDataCommand.Actor,
                InsertFuturesTickTradeDataCommand.Verb, entity.Format()),
            EntityId = entity
        };
        var auditLogger = (ICommandAuditLogger)fixture.ActorEventDb;

        var legacyAudit = fixture.ActorEventDb.InsertCommandLogAsync(
            command,
            DateTime.UtcNow,
            "payload");
        (await auditLogger.TryReserveAsync(command)).Accepted.Should().BeTrue();
        await legacyAudit;

        var duplicateAudit = fixture.ActorEventDb.InsertCommandLogAsync(
            command,
            DateTime.UtcNow,
            "payload");
        (await auditLogger.TryReserveAsync(command)).Accepted.Should().BeFalse();
        await duplicateAudit;
    }

    [Fact]
    public async Task Independent_process_caches_still_have_one_postgres_winner()
    {
        var entity = new TickDataEntityId("ESU6", new DateOnly(2026, 8, 7), AssetTypeId.Futures);
        var command = new InsertFuturesTickTradeDataCommand
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesTickTradeDataCommand.Actor,
                InsertFuturesTickTradeDataCommand.Verb, entity.Format()),
            EntityId = entity
        };
        var firstProcess = fixture.CreateActorEventDb();
        var secondProcess = fixture.CreateActorEventDb();

        var attempts = Enumerable.Range(0, 32)
            .Select(index => (index & 1) == 0
                ? firstProcess.TryInsertCommandLogAsync(command, DateTime.UtcNow, "payload")
                : secondProcess.TryInsertCommandLogAsync(command, DateTime.UtcNow, "payload"));
        var results = await Task.WhenAll(attempts);

        results.Count(accepted => accepted).Should().Be(1);
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
    public async Task Projector_claim_blocks_later_same_stream_event_until_predecessor_is_terminal()
    {
        var stream = NewStream();
        var saved = (await fixture.ActorEventDb.SaveEventsAsync(
            stream,
            Guid.NewGuid(),
            new DomainEventCollection([RangeEvent(), RangeEvent()])))
            .OrderBy(item => item.EventId)
            .ToArray();
        var streamId = await fixture.ActorEventDb.GetEventStreamIdAsync(stream);
        var projectorName = $"OrderedProjector.{Guid.NewGuid():N}";
        var nowUtc = DateTime.UtcNow;
        foreach (var sourceEvent in saved)
        {
            (await fixture.ActorEventDb.TryCreateEventProjectorExecutionStateAsync(
                NewExecutionState(sourceEvent.EventId, streamId, projectorName, nowUtc))).Should().NotBeNull();
        }
        var initialSnapshot = await fixture.ActorEventDb.GetEventProjectorOperationalSnapshotAsync(
            projectorName, nowUtc);
        initialSnapshot.PendingCount.Should().Be(2);
        initialSnapshot.OutboxPendingCount.Should().Be(0);

        (await fixture.ActorEventDb.TryClaimEventProjectorExecutionAsync(
            saved[1].EventId, projectorName, Guid.NewGuid(), nowUtc, TimeSpan.FromMinutes(2)))
            .Should().BeNull();
        (await fixture.ActorEventDb.HasEarlierUnresolvedEventProjectorExecutionAsync(
            saved[1].EventId, projectorName)).Should().BeTrue();

        var firstToken = Guid.NewGuid();
        var first = await fixture.ActorEventDb.TryClaimEventProjectorExecutionAsync(
            saved[0].EventId, projectorName, firstToken, nowUtc, TimeSpan.FromMinutes(2));
        first.Should().NotBeNull();
        (await fixture.ActorEventDb.TryTerminalizeEventProjectorExecutionAsync(
            new EventProjectorStateTransition(
                first!.EventId,
                projectorName,
                firstToken,
                first.Revision,
                first.Stage,
                EventProjectorStageType.Completed,
                EventProjectorOutcomeType.Completed,
                first.Stage),
            nowUtc.AddSeconds(1))).Should().NotBeNull();

        (await fixture.ActorEventDb.HasEarlierUnresolvedEventProjectorExecutionAsync(
            saved[1].EventId, projectorName)).Should().BeFalse();
        (await fixture.ActorEventDb.TryClaimEventProjectorExecutionAsync(
            saved[1].EventId, projectorName, Guid.NewGuid(), nowUtc.AddSeconds(1), TimeSpan.FromMinutes(2)))
            .Should().NotBeNull();
    }

    [Fact]
    public async Task Applied_checkpoint_does_not_skip_pending_terminal_publication_or_release_stream_order()
    {
        var stream = NewStream();
        var saved = (await fixture.ActorEventDb.SaveEventsAsync(
            stream,
            Guid.NewGuid(),
            new DomainEventCollection([RangeEvent(), RangeEvent()])))
            .OrderBy(item => item.EventId)
            .ToArray();
        var streamId = await fixture.ActorEventDb.GetEventStreamIdAsync(stream);
        var projectorName = $"PostApplyOrderingProjector.{Guid.NewGuid():N}";
        var nowUtc = DateTime.UtcNow;
        foreach (var sourceEvent in saved)
        {
            (await fixture.ActorEventDb.TryCreateEventProjectorExecutionStateAsync(
                NewExecutionState(sourceEvent.EventId, streamId, projectorName, nowUtc))).Should().NotBeNull();
        }

        var firstToken = Guid.NewGuid();
        var first = await fixture.ActorEventDb.TryClaimEventProjectorExecutionAsync(
            saved[0].EventId, projectorName, firstToken, nowUtc, TimeSpan.FromMinutes(2));
        first = await fixture.ActorEventDb.TryTransitionEventProjectorExecutionAsync(
            new EventProjectorStateTransition(
                first!.EventId,
                projectorName,
                firstToken,
                first.Revision,
                EventProjectorStageType.ValidateSourceEvent,
                EventProjectorStageType.ApplyProjection,
                EventProjectorOutcomeType.Processing,
                EventProjectorStageType.ValidateSourceEvent),
            nowUtc.AddSeconds(1));
        first = await fixture.ActorEventDb.TryTransitionEventProjectorExecutionAsync(
            new EventProjectorStateTransition(
                first!.EventId,
                projectorName,
                firstToken,
                first.Revision,
                EventProjectorStageType.ApplyProjection,
                EventProjectorStageType.PublishCompletedEvent,
                EventProjectorOutcomeType.Processing,
                EventProjectorStageType.ApplyProjection),
            nowUtc.AddSeconds(2));
        first.Should().NotBeNull();
        (await fixture.ActorEventDb.GetEventProjectorStreamCheckpointAsync(projectorName, streamId))!
            .LastAppliedStreamVersion.Should().Be(1);

        (await fixture.ActorEventDb.HasEarlierUnresolvedEventProjectorExecutionAsync(
            saved[1].EventId, projectorName)).Should().BeTrue();
        (await fixture.ActorEventDb.TryClaimEventProjectorExecutionAsync(
            saved[1].EventId,
            projectorName,
            Guid.NewGuid(),
            nowUtc.AddSeconds(3),
            TimeSpan.FromMinutes(2))).Should().BeNull();

        var resumedToken = Guid.NewGuid();
        first = await fixture.ActorEventDb.TryClaimEventProjectorExecutionAsync(
            saved[0].EventId,
            projectorName,
            resumedToken,
            nowUtc.AddMinutes(3),
            TimeSpan.FromMinutes(2));
        first.Should().NotBeNull();
        first!.Stage.Should().Be(EventProjectorStageType.PublishCompletedEvent);
        first.Outcome.Should().Be(EventProjectorOutcomeType.Processing);

        first = await fixture.ActorEventDb.TryTerminalizeEventProjectorExecutionAsync(
            new EventProjectorStateTransition(
                first!.EventId,
                projectorName,
                resumedToken,
                first.Revision,
                EventProjectorStageType.PublishCompletedEvent,
                EventProjectorStageType.Completed,
                EventProjectorOutcomeType.Completed,
                EventProjectorStageType.PublishCompletedEvent),
            nowUtc.AddMinutes(3).AddSeconds(1));
        first.Should().NotBeNull();
        (await fixture.ActorEventDb.TryClaimEventProjectorExecutionAsync(
            saved[1].EventId,
            projectorName,
            Guid.NewGuid(),
            nowUtc.AddMinutes(3).AddSeconds(2),
            TimeSpan.FromMinutes(2))).Should().NotBeNull();
    }

    [Fact]
    public async Task Projector_release_makes_a_failed_stage_immediately_claimable_by_a_new_owner()
    {
        var stream = NewStream();
        var saved = await fixture.ActorEventDb.SaveEventsAsync(
            stream,
            Guid.NewGuid(),
            new DomainEventCollection([RangeEvent()]));
        var persisted = saved.Single();
        var streamId = await fixture.ActorEventDb.GetEventStreamIdAsync(stream);
        var projectorName = $"ReleaseProjector.{Guid.NewGuid():N}";
        var nowUtc = DateTime.UtcNow;
        (await fixture.ActorEventDb.TryCreateEventProjectorExecutionStateAsync(
            NewExecutionState(persisted.EventId, streamId, projectorName, nowUtc))).Should().NotBeNull();

        var firstToken = Guid.NewGuid();
        var firstOwner = await fixture.ActorEventDb.TryClaimEventProjectorExecutionAsync(
            persisted.EventId,
            projectorName,
            firstToken,
            nowUtc,
            TimeSpan.FromMinutes(2));
        firstOwner.Should().NotBeNull();
        var retryAtUtc = nowUtc.AddSeconds(30);
        var released = await fixture.ActorEventDb.TryReleaseEventProjectorExecutionAsync(
            new EventProjectorStateTransition(
                persisted.EventId,
                projectorName,
                firstToken,
                firstOwner!.Revision,
                firstOwner.Stage,
                firstOwner.Stage,
                EventProjectorOutcomeType.Retrying,
                firstOwner.LastCompletedStage,
                1,
                retryAtUtc,
                nowUtc.AddSeconds(1),
                "injected target failure"),
            nowUtc.AddSeconds(1));

        released.Should().NotBeNull();
        released!.ExecutionToken.Should().BeNull();
        released.LeaseExpiresAtUtc.Should().BeNull();
        released.Outcome.Should().Be(EventProjectorOutcomeType.Retrying);
        released.RetryCount.Should().Be(1);
        released.NextAttemptAtUtc.Should().BeCloseTo(retryAtUtc, TimeSpan.FromMilliseconds(1));

        var secondToken = Guid.NewGuid();
        var secondOwner = await fixture.ActorEventDb.TryClaimEventProjectorExecutionAsync(
            persisted.EventId,
            projectorName,
            secondToken,
            nowUtc.AddSeconds(2),
            TimeSpan.FromMinutes(2));
        secondOwner.Should().NotBeNull();
        secondOwner!.ExecutionToken.Should().Be(secondToken);
        secondOwner.Revision.Should().Be(released.Revision + 1);
    }

    [Fact]
    public async Task Projector_state_transition_and_outbox_insert_are_atomic_and_dispatch_is_leased()
    {
        var stream = NewStream();
        var saved = await fixture.ActorEventDb.SaveEventsAsync(
            stream,
            Guid.NewGuid(),
            new DomainEventCollection([RangeEvent()]));
        var persisted = saved.Single();
        var streamId = await fixture.ActorEventDb.GetEventStreamIdAsync(stream);
        var projectorName = $"OutboxProjector.{Guid.NewGuid():N}";
        var nowUtc = DateTime.UtcNow;
        (await fixture.ActorEventDb.TryCreateEventProjectorExecutionStateAsync(
            NewExecutionState(persisted.EventId, streamId, projectorName, nowUtc))).Should().NotBeNull();
        var ownerToken = Guid.NewGuid();
        var owner = await fixture.ActorEventDb.TryClaimEventProjectorExecutionAsync(
            persisted.EventId,
            projectorName,
            ownerToken,
            nowUtc,
            TimeSpan.FromMinutes(2));
        owner.Should().NotBeNull();
        var identity = new EventProjectorEffectIdentity(
            projectorName,
            persisted.EventId,
            EventProjectorEffectKind.ProcessingPublication);
        var transitioned = await fixture.ActorEventDb.TryTransitionEventProjectorExecutionWithOutboxAsync(
            new EventProjectorStateTransition(
                persisted.EventId,
                projectorName,
                ownerToken,
                owner!.Revision,
                owner.Stage,
                EventProjectorStageType.ApplyProjection,
                EventProjectorOutcomeType.Processing,
                EventProjectorStageType.PublishProcessingEvent),
            new EventProjectorOutboxMessage(identity, typeof(FuturesRsiSignalGeneratedEvent).AssemblyQualifiedName!, [1, 2, 3]),
            nowUtc.AddSeconds(1));

        transitioned.Should().NotBeNull();
        transitioned!.Stage.Should().Be(EventProjectorStageType.ApplyProjection);
        var firstDispatchToken = Guid.NewGuid();
        var claimed = await fixture.ActorEventDb.ClaimEventProjectorOutboxAsync(
            projectorName,
            firstDispatchToken,
            nowUtc.AddSeconds(2),
            TimeSpan.FromMinutes(1),
            8);
        claimed.Should().ContainSingle();
        claimed[0].MessageId.Should().Be(identity.MessageId);
        claimed[0].DispatchToken.Should().Be(firstDispatchToken);

        var concurrentClaim = await fixture.ActorEventDb.ClaimEventProjectorOutboxAsync(
            projectorName,
            Guid.NewGuid(),
            nowUtc.AddSeconds(3),
            TimeSpan.FromMinutes(1),
            8);
        concurrentClaim.Should().BeEmpty();
        (await fixture.ActorEventDb.MarkEventProjectorOutboxPublishedAsync(
            claimed[0], nowUtc.AddSeconds(4))).Should().BeTrue();
        (await fixture.ActorEventDb.ClaimEventProjectorOutboxAsync(
            projectorName,
            Guid.NewGuid(),
            nowUtc.AddSeconds(5),
            TimeSpan.FromMinutes(1),
            8)).Should().BeEmpty();
    }

    [Fact]
    public async Task Publish_after_apply_transition_advances_checkpoint_with_the_atomic_outbox_write()
    {
        var stream = NewStream();
        var persisted = (await fixture.ActorEventDb.SaveEventsAsync(
            stream,
            Guid.NewGuid(),
            new DomainEventCollection([RangeEvent()]))).Single();
        var streamId = await fixture.ActorEventDb.GetEventStreamIdAsync(stream);
        var projectorName = $"AfterApplyProjector.{Guid.NewGuid():N}";
        var nowUtc = DateTime.UtcNow;
        var state = await fixture.ActorEventDb.TryCreateEventProjectorExecutionStateAsync(
            NewExecutionState(persisted.EventId, streamId, projectorName, nowUtc));
        var token = Guid.NewGuid();
        state = await fixture.ActorEventDb.TryClaimEventProjectorExecutionAsync(
            persisted.EventId, projectorName, token, nowUtc, TimeSpan.FromMinutes(2));
        state = await fixture.ActorEventDb.TryTransitionEventProjectorExecutionAsync(
            new EventProjectorStateTransition(
                persisted.EventId,
                projectorName,
                token,
                state!.Revision,
                EventProjectorStageType.ValidateSourceEvent,
                EventProjectorStageType.ApplyProjection,
                EventProjectorOutcomeType.Processing,
                EventProjectorStageType.ValidateSourceEvent),
            nowUtc.AddSeconds(1));
        var identity = new EventProjectorEffectIdentity(
            projectorName,
            persisted.EventId,
            EventProjectorEffectKind.ProcessingPublication);

        var afterApply = await fixture.ActorEventDb.TryTransitionEventProjectorExecutionWithOutboxAsync(
            new EventProjectorStateTransition(
                persisted.EventId,
                projectorName,
                token,
                state!.Revision,
                EventProjectorStageType.ApplyProjection,
                EventProjectorStageType.PublishCompletedEvent,
                EventProjectorOutcomeType.Processing,
                EventProjectorStageType.PublishProcessingEvent),
            new EventProjectorOutboxMessage(
                identity,
                typeof(FuturesRsiSignalGeneratedEvent).AssemblyQualifiedName!,
                [4, 5, 6]),
            nowUtc.AddSeconds(2));

        afterApply.Should().NotBeNull();
        var checkpoint = await fixture.ActorEventDb.GetEventProjectorStreamCheckpointAsync(
            projectorName, streamId);
        checkpoint.Should().NotBeNull();
        checkpoint!.LastAppliedStreamVersion.Should().Be(1);
        checkpoint.LastAppliedEventId.Should().Be(persisted.EventId);
        (await fixture.ActorEventDb.ClaimEventProjectorOutboxAsync(
            projectorName,
            Guid.NewGuid(),
            nowUtc.AddSeconds(3),
            TimeSpan.FromMinutes(1),
            1)).Should().ContainSingle(message => message.MessageId == identity.MessageId);
    }

    [Fact]
    public async Task Projector_operator_pages_retry_exact_and_skip_with_reason_are_durable()
    {
        var stream = NewStream();
        var persisted = (await fixture.ActorEventDb.SaveEventsAsync(
            stream, Guid.NewGuid(), new DomainEventCollection([RangeEvent()]))).Single();
        var streamId = await fixture.ActorEventDb.GetEventStreamIdAsync(stream);
        var projectorName = $"OperatorProjector.{Guid.NewGuid():N}";
        var nowUtc = DateTime.UtcNow;
        (await fixture.ActorEventDb.TryCreateEventProjectorExecutionStateAsync(
            NewExecutionState(persisted.EventId, streamId, projectorName, nowUtc))).Should().NotBeNull();
        var token = Guid.NewGuid();
        var claimed = await fixture.ActorEventDb.TryClaimEventProjectorExecutionAsync(
            persisted.EventId, projectorName, token, nowUtc, TimeSpan.FromMinutes(2));
        var failed = await fixture.ActorEventDb.TryTerminalizeEventProjectorExecutionAsync(
            new EventProjectorStateTransition(
                persisted.EventId, projectorName, token, claimed!.Revision, claimed.Stage,
                EventProjectorStageType.Completed, EventProjectorOutcomeType.Failed,
                EventProjectorStageType.None, 3, LastErrorAtUtc: nowUtc.AddSeconds(1),
                ErrorMessage: "injected", BlockedReason: string.Empty),
            nowUtc.AddSeconds(1));
        failed.Should().NotBeNull();
        (await fixture.ActorEventDb.GetEventProjectorOperationalStatePageAsync(
            projectorName, EventProjectorOperationalStatus.Failed, 0, 8))
            .Should().ContainSingle(state => state.EventId == persisted.EventId);

        var retry = await fixture.ActorEventDb.TryRetryEventProjectorExecutionAsync(
            persisted.EventId,
            projectorName,
            nowUtc.AddSeconds(2));
        retry.Should().NotBeNull();
        retry!.Outcome.Should().Be(EventProjectorOutcomeType.Retrying);
        retry.Stage.Should().Be(EventProjectorStageType.ValidateSourceEvent);
        retry.RetryCount.Should().Be(0);
        (await fixture.ActorEventDb.GetEventProjectorOperationalStatePageAsync(
            projectorName, EventProjectorOperationalStatus.Pending, 0, 8))
            .Should().ContainSingle(state => state.EventId == persisted.EventId);

        var skipped = await fixture.ActorEventDb.TrySkipEventProjectorExecutionAsync(
            persisted.EventId,
            projectorName,
            "source event intentionally ignored by operator",
            nowUtc.AddSeconds(3));
        skipped.Should().NotBeNull();
        skipped!.Outcome.Should().Be(EventProjectorOutcomeType.Superseded);
        skipped.BlockedReason.Should().Be("operator-skip:source event intentionally ignored by operator");
        (await fixture.ActorEventDb.GetEventProjectorOperationalStatePageAsync(
            projectorName, EventProjectorOperationalStatus.Blocked, 0, 8))
            .Should().ContainSingle(state => state.EventId == persisted.EventId);
        var retrySkipped = await fixture.ActorEventDb.TryRetryEventProjectorExecutionAsync(
            persisted.EventId,
            projectorName,
            nowUtc.AddSeconds(4));
        retrySkipped.Should().NotBeNull();
        retrySkipped!.Stage.Should().Be(EventProjectorStageType.ValidateSourceEvent);
        retrySkipped.BlockedReason.Should().BeEmpty();
    }

    [Fact]
    public async Task Legacy_projector_upsert_populates_additive_stream_identity_for_bounded_recovery()
    {
        var stream = NewStream();
        var saved = await fixture.ActorEventDb.SaveEventsAsync(
            stream,
            Guid.NewGuid(),
            new DomainEventCollection([RangeEvent()]));
        var persisted = saved.Single();
        var streamId = await fixture.ActorEventDb.GetEventStreamIdAsync(stream);
        var projectorName = $"LegacyCompatibilityProjector.{Guid.NewGuid():N}";
        var nowUtc = DateTime.UtcNow;

        await fixture.ActorEventDb.InsertEventProjectorStateAsync(new EventProjectorStateReadModel(
            persisted.EventId,
            "LegacyActor",
            projectorName,
            false,
            0,
            EventProjectorOutcomeType.Processing,
            EventProjectorStageType.PublishProcessingEvent,
            createdTimestamp: nowUtc,
            updatedTimestamp: nowUtc));

        var execution = await fixture.ActorEventDb.GetEventProjectorExecutionStateAsync(
            persisted.EventId,
            projectorName);
        execution.Should().NotBeNull();
        execution!.EventStreamId.Should().Be(streamId);
        execution.SourceEventName.Should().Be(nameof(FuturesRsiSignalGeneratedEvent));
        execution.Revision.Should().Be(0);
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

    [Fact]
    public async Task Concurrent_appends_assign_unique_contiguous_versions_within_each_stream()
    {
        var stream = NewStream();
        const int appendCount = 16;

        await Task.WhenAll(Enumerable.Range(0, appendCount).Select(_ =>
            fixture.ActorEventDb.SaveEventsAsync(
                stream,
                Guid.NewGuid(),
                new DomainEventCollection([RangeEvent()]))));

        var streamId = await fixture.ActorEventDb.GetEventStreamIdAsync(stream);
        var persisted = await fixture.ActorEventDb.LoadActorEventStreamAsync<TestActorState>(streamId);

        persisted.Should().HaveCount(appendCount);
        persisted.Select(item => item.StreamVersion)
            .Should().BeEquivalentTo(Enumerable.Range(1, appendCount).Select(value => (long)value));
        persisted.Select(item => item.StreamVersion).Should().OnlyHaveUniqueItems();
        persisted.OrderBy(item => item.EventVersion).Select(item => item.StreamVersion)
            .Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Expected_version_batch_commits_all_events_with_contiguous_stream_versions()
    {
        var stream = NewStream();

        var saved = await fixture.ActorEventDb.SaveEventsAsync(
            stream,
            Guid.NewGuid(),
            new DomainEventCollection([RangeEvent(), RangeEvent()]),
            expectedStreamVersion: 0,
            CancellationToken.None);

        saved.Should().HaveCount(2);
        var persisted = await fixture.ActorEventDb.LoadActorEventStreamAsync<TestActorState>(
            await fixture.ActorEventDb.GetEventStreamIdAsync(stream));
        persisted.Select(item => item.StreamVersion).Should().Equal(1L, 2L);
    }

    [Fact]
    public async Task Stale_expected_version_rejects_the_complete_event_batch()
    {
        var stream = NewStream();
        await fixture.ActorEventDb.SaveEventsAsync(
            stream,
            Guid.NewGuid(),
            new DomainEventCollection([RangeEvent()]),
            expectedStreamVersion: 0,
            CancellationToken.None);

        var append = async () => await fixture.ActorEventDb.SaveEventsAsync(
            stream,
            Guid.NewGuid(),
            new DomainEventCollection([RangeEvent(), RangeEvent()]),
            expectedStreamVersion: 0,
            CancellationToken.None);

        await append.Should().ThrowAsync<ConcurrencyException>();
        var persisted = await fixture.ActorEventDb.LoadActorEventStreamAsync<TestActorState>(
            await fixture.ActorEventDb.GetEventStreamIdAsync(stream));
        persisted.Should().ContainSingle();
        persisted.Single().StreamVersion.Should().Be(1);
    }

    [Fact]
    public async Task Stream_versions_are_independent_while_global_event_ids_remain_unique()
    {
        var firstStream = NewStream();
        var secondStream = NewStream();
        await SaveAsync(firstStream, RangeEvent(), RangeEvent());
        await SaveAsync(secondStream, RangeEvent(), RangeEvent());

        var first = await fixture.ActorEventDb.LoadActorEventStreamAsync<TestActorState>(
            await fixture.ActorEventDb.GetEventStreamIdAsync(firstStream));
        var second = await fixture.ActorEventDb.LoadActorEventStreamAsync<TestActorState>(
            await fixture.ActorEventDb.GetEventStreamIdAsync(secondStream));

        first.Select(item => item.StreamVersion).Should().Equal(1L, 2L);
        second.Select(item => item.StreamVersion).Should().Equal(1L, 2L);
        first.Concat(second).Select(item => item.EventVersion).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Applied_checkpoint_suppresses_an_older_projection_state_created_afterward()
    {
        var stream = NewStream();
        var saved = (await fixture.ActorEventDb.SaveEventsAsync(
            stream,
            Guid.NewGuid(),
            new DomainEventCollection([RangeEvent(), RangeEvent()])))
            .OrderBy(item => item.EventId)
            .ToArray();
        var streamId = await fixture.ActorEventDb.GetEventStreamIdAsync(stream);
        var projectorName = $"CheckpointProjector.{Guid.NewGuid():N}";
        var nowUtc = DateTime.UtcNow;

        var newer = await fixture.ActorEventDb.TryCreateEventProjectorExecutionStateAsync(
            NewExecutionState(saved[1].EventId, streamId, projectorName, nowUtc));
        newer.Should().NotBeNull();
        newer!.StreamVersion.Should().Be(2);
        (await fixture.ActorEventDb.GetEventProjectorStreamCheckpointAsync(projectorName, streamId))
            .Should().BeNull();

        var token = Guid.NewGuid();
        newer = await fixture.ActorEventDb.TryClaimEventProjectorExecutionAsync(
            newer.EventId, projectorName, token, nowUtc, TimeSpan.FromMinutes(2));
        newer = await fixture.ActorEventDb.TryTransitionEventProjectorExecutionAsync(
            new EventProjectorStateTransition(
                newer!.EventId, projectorName, token, newer.Revision, newer.Stage,
                EventProjectorStageType.ApplyProjection, EventProjectorOutcomeType.Processing,
                EventProjectorStageType.ValidateSourceEvent),
            nowUtc.AddSeconds(1));
        newer = await fixture.ActorEventDb.TryTransitionEventProjectorExecutionAsync(
            new EventProjectorStateTransition(
                newer!.EventId, projectorName, token, newer.Revision, newer.Stage,
                EventProjectorStageType.PublishCompletedEvent, EventProjectorOutcomeType.Processing,
                EventProjectorStageType.ApplyProjection),
            nowUtc.AddSeconds(2));

        var checkpoint = await fixture.ActorEventDb.GetEventProjectorStreamCheckpointAsync(
            projectorName, streamId);
        checkpoint.Should().NotBeNull();
        checkpoint!.LastAppliedStreamVersion.Should().Be(2);
        checkpoint.LastAppliedEventId.Should().Be(saved[1].EventId);

        var stale = await fixture.ActorEventDb.TryCreateEventProjectorExecutionStateAsync(
            NewExecutionState(saved[0].EventId, streamId, projectorName, nowUtc.AddSeconds(3)));
        stale.Should().NotBeNull();
        stale!.StreamVersion.Should().Be(1);
        stale.Stage.Should().Be(EventProjectorStageType.Completed);
        stale.Outcome.Should().Be(EventProjectorOutcomeType.Superseded);
        stale.ExecutionToken.Should().BeNull();
    }

    [Fact]
    public async Task Source_only_projection_terminalization_advances_the_stream_checkpoint_atomically()
    {
        var stream = NewStream();
        var persisted = (await fixture.ActorEventDb.SaveEventsAsync(
            stream,
            Guid.NewGuid(),
            new DomainEventCollection([RangeEvent()]))).Single();
        var streamId = await fixture.ActorEventDb.GetEventStreamIdAsync(stream);
        var projectorName = $"SourceOnlyProjector.{Guid.NewGuid():N}";
        var nowUtc = DateTime.UtcNow;
        var state = await fixture.ActorEventDb.TryCreateEventProjectorExecutionStateAsync(
            NewExecutionState(persisted.EventId, streamId, projectorName, nowUtc));
        var token = Guid.NewGuid();
        state = await fixture.ActorEventDb.TryClaimEventProjectorExecutionAsync(
            persisted.EventId, projectorName, token, nowUtc, TimeSpan.FromMinutes(2));
        state = await fixture.ActorEventDb.TryTransitionEventProjectorExecutionAsync(
            new EventProjectorStateTransition(
                persisted.EventId,
                projectorName,
                token,
                state!.Revision,
                EventProjectorStageType.ValidateSourceEvent,
                EventProjectorStageType.ApplyProjection,
                EventProjectorOutcomeType.Processing,
                EventProjectorStageType.ValidateSourceEvent),
            nowUtc.AddSeconds(1));

        var completed = await fixture.ActorEventDb.TryTerminalizeEventProjectorExecutionAsync(
            new EventProjectorStateTransition(
                persisted.EventId,
                projectorName,
                token,
                state!.Revision,
                EventProjectorStageType.ApplyProjection,
                EventProjectorStageType.Completed,
                EventProjectorOutcomeType.Completed,
                EventProjectorStageType.ApplyProjection),
            nowUtc.AddSeconds(2));

        completed.Should().NotBeNull();
        completed!.Outcome.Should().Be(EventProjectorOutcomeType.Completed);
        var checkpoint = await fixture.ActorEventDb.GetEventProjectorStreamCheckpointAsync(
            projectorName, streamId);
        checkpoint.Should().NotBeNull();
        checkpoint!.LastAppliedStreamVersion.Should().Be(1);
        checkpoint.LastAppliedEventId.Should().Be(persisted.EventId);
    }

    [Fact]
    public async Task Claim_reconciles_a_preexisting_retried_state_when_a_newer_stream_version_is_checkpointed()
    {
        var stream = NewStream();
        var saved = (await fixture.ActorEventDb.SaveEventsAsync(
            stream,
            Guid.NewGuid(),
            new DomainEventCollection([RangeEvent(), RangeEvent()])))
            .OrderBy(item => item.EventId)
            .ToArray();
        var streamId = await fixture.ActorEventDb.GetEventStreamIdAsync(stream);
        var projectorName = $"CheckpointRetryProjector.{Guid.NewGuid():N}";
        var nowUtc = DateTime.UtcNow;

        var older = await fixture.ActorEventDb.TryCreateEventProjectorExecutionStateAsync(
            NewExecutionState(saved[0].EventId, streamId, projectorName, nowUtc));
        older.Should().NotBeNull();
        (await fixture.ActorEventDb.TrySkipEventProjectorExecutionAsync(
            older!.EventId, projectorName, "operator deferred older projection", nowUtc.AddSeconds(1)))
            .Should().NotBeNull();

        var newer = await fixture.ActorEventDb.TryCreateEventProjectorExecutionStateAsync(
            NewExecutionState(saved[1].EventId, streamId, projectorName, nowUtc.AddSeconds(2)));
        var token = Guid.NewGuid();
        newer = await fixture.ActorEventDb.TryClaimEventProjectorExecutionAsync(
            newer!.EventId, projectorName, token, nowUtc.AddSeconds(2), TimeSpan.FromMinutes(2));
        newer = await fixture.ActorEventDb.TryTransitionEventProjectorExecutionAsync(
            new EventProjectorStateTransition(
                newer!.EventId, projectorName, token, newer.Revision, newer.Stage,
                EventProjectorStageType.ApplyProjection, EventProjectorOutcomeType.Processing,
                EventProjectorStageType.ValidateSourceEvent),
            nowUtc.AddSeconds(3));
        newer = await fixture.ActorEventDb.TryTransitionEventProjectorExecutionAsync(
            new EventProjectorStateTransition(
                newer!.EventId, projectorName, token, newer.Revision, newer.Stage,
                EventProjectorStageType.PublishCompletedEvent, EventProjectorOutcomeType.Processing,
                EventProjectorStageType.ApplyProjection),
            nowUtc.AddSeconds(4));
        newer.Should().NotBeNull();

        var retriedOlder = await fixture.ActorEventDb.TryRetryEventProjectorExecutionAsync(
            older.EventId, projectorName, nowUtc.AddSeconds(5));
        retriedOlder.Should().NotBeNull();
        retriedOlder!.Outcome.Should().Be(EventProjectorOutcomeType.Retrying);

        var reconciled = await fixture.ActorEventDb.TryClaimEventProjectorExecutionAsync(
            older.EventId, projectorName, Guid.NewGuid(), nowUtc.AddSeconds(6), TimeSpan.FromMinutes(2));
        reconciled.Should().NotBeNull();
        reconciled!.Stage.Should().Be(EventProjectorStageType.Completed);
        reconciled.Outcome.Should().Be(EventProjectorOutcomeType.Superseded);
        reconciled.BlockedReason.Should().Be("stream-checkpoint-covered");
        reconciled.ExecutionToken.Should().BeNull();
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
