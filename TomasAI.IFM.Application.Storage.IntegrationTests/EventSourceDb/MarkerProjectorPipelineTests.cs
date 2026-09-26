using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class MarkerProjectorFixture
{
    public string Direct { get; }
    public string Provider { get; }
    public EventSourceActorSnapshotRangeFixture Storage { get; }
    public IBlackboardService Blackboard { get; }
    public MarkerProjectorFixture()
    {
        Direct = Environment.GetEnvironmentVariable("IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION")
            ?? throw new InvalidOperationException("An explicit disposable benchmark database is required.");
        var builder = new NpgsqlConnectionStringBuilder(Direct);
        if (builder.Host != "127.0.0.1" || builder.Port != 25432)
            throw new InvalidOperationException("Projector qualification requires isolated loopback port 25432.");
        builder.Username = ""; builder.Password = "";
        Provider = builder.ConnectionString;
        _ = EventLogSqlLayout.ForBenchmark(Provider, false, true); // Validate before schema creation.
        var schema = Environment.GetEnvironmentVariable("IFM_PROJECTOR_SCHEMA_TEST");
        if (schema is not (null or "three-index"))
            throw new InvalidOperationException("Unknown isolated projector schema test mode.");
        Storage = new EventSourceActorSnapshotRangeFixture();
        var cache = Substitute.For<IRedisCache>();
        var values = new ConcurrentDictionary<string, string>();
        cache.TryGet(Arg.Any<string>(), out Arg.Any<string>()).Returns(call =>
        {
            var found = values.TryGetValue(call.ArgAt<string>(0), out var value);
            call[1] = value!;
            return found;
        });
        cache.When(c => c.Set(Arg.Any<string>(), Arg.Any<string>()))
            .Do(call => values[call.ArgAt<string>(0)] = call.ArgAt<string>(1));
        Blackboard = new BlackboardService(cache, new SystemTextJsonSerializer());
        using var db = new NpgsqlConnection(Direct);
        db.Open();
        if (schema == "three-index")
        {
            using var indexes = new NpgsqlCommand("""
                ALTER TABLE event_log DROP CONSTRAINT event_log_pkey;
                ALTER TABLE event_log ADD CONSTRAINT ux_event_log_stream_version_v3
                    PRIMARY KEY USING INDEX ux_event_log_stream_version_v3;
                """, db);
            indexes.ExecuteNonQuery();
        }
        using (var shape = new NpgsqlCommand("""
            SELECT pg_get_constraintdef(oid) FROM pg_constraint
            WHERE conrelid='event_log'::regclass AND contype='p'
            """, db))
            Assert.Equal(schema == "three-index"
                ? "PRIMARY KEY (eventstreamid, streamversion)"
                : "PRIMARY KEY (eventstreamid, eventnameid, eventversion)", (string)shape.ExecuteScalar()!);
        using (var count = new NpgsqlCommand("""
            SELECT count(*) FROM pg_indexes WHERE schemaname='public' AND tablename='event_log'
            """, db))
            Assert.Equal(schema == "three-index" ? 3L : 4L, (long)count.ExecuteScalar()!);
        using var ddl = new NpgsqlCommand("""
            CREATE TABLE marker_probe_receipt(projector text NOT NULL,eventid bigint NOT NULL,
                messageid text NOT NULL,PRIMARY KEY(projector,eventid));
            CREATE TABLE marker_probe_current(projector text NOT NULL,streamid bigint NOT NULL,
                lastvalue bigint NOT NULL,mutations bigint NOT NULL,PRIMARY KEY(projector,streamid))
            """, db);
        ddl.ExecuteNonQuery();
    }
    public async Task<object?> Sql(string sql, params object[] parameters)
    {
        await using var db = new NpgsqlConnection(Direct);
        await db.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, db) { CommandTimeout = 10 };
        foreach (var value in parameters) cmd.Parameters.AddWithValue(value);
        return await cmd.ExecuteScalarAsync();
    }
}

/// <summary>
/// Real appender, PostgreSQL state/leases, production BaseEventProjector engine and recovery coordinator.
/// Delivery is a test callback; no claim of actor mailbox, NATS transport or terminal publication coverage.
/// </summary>
public sealed partial class MarkerProjectorPipelineTests(MarkerProjectorFixture fixture) : IClassFixture<MarkerProjectorFixture>
{
    static EventProjectorReliabilityOptions Options => new()
    {
        FencedExecutionEnabled = true, BoundedRecoveryEnabled = true,
        RecoveryBatchSize = 7, RecoveryStreamConcurrency = 4, InitialReplayDelay = TimeSpan.FromMilliseconds(1)
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Multistream_recovery_projects_every_event_and_restart_has_no_remaining_work(bool batched)
    {
        var name = "MarkerPipeline." + Guid.NewGuid().ToString("N");
        var streams = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Append(batched, name, 16)));
        var projector = Projector(name);
        var recovered = await Recover(projector);
        Assert.Equal(128, recovered.Discovered);
        Assert.Equal(128, recovered.Queued);
        foreach (var stream in streams) await VerifyCompleted(stream, 16);
        var restarted = Projector(name);
        var second = await Recover(restarted);
        Assert.Equal(0, second.Discovered);
        foreach (var stream in streams)
        {
            foreach (var domainEvent in stream.Events)
                await restarted.ProcessDomainEventAsync(domainEvent);
            await VerifyCompleted(stream, 16);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Failure_before_or_after_target_commit_recovers_without_duplicate_mutation(bool batched, bool afterTarget)
    {
        var name = "MarkerPipeline." + Guid.NewGuid().ToString("N");
        var stream = await Append(batched, name, 2);
        var first = Projector(name, failBefore: !afterTarget, failAfter: afterTarget);
        await Assert.ThrowsAsync<InjectedFailure>(() => first.ProcessDomainEventAsync(stream.Events[0]).AsTask());
        var state = await fixture.Storage.ActorEventDb.GetEventProjectorExecutionStateAsync(stream.Events[0].EventId, name);
        Assert.Equal(EventProjectorOutcomeType.Retrying, state!.Outcome);
        Assert.Null(state.ExecutionToken);
        Assert.Null(await fixture.Storage.ActorEventDb.GetEventProjectorStreamCheckpointAsync(name, stream.Id));
        Assert.Equal(afterTarget ? 1L : 0L, Convert.ToInt64(await fixture.Sql(
            "SELECT count(*) FROM marker_probe_receipt WHERE projector=$1", name)));
        await Task.Delay(20); // Exceeds the explicitly configured test retry delay, not a production interval.
        var restarted = Projector(name);
        var recovered = await Recover(restarted);
        Assert.Equal(2, recovered.Queued);
        await VerifyCompleted(stream, 2);
        Assert.Equal(afterTarget ? 1 : 0, restarted.AlreadyApplied);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Later_stream_event_waits_for_earlier_marker_to_complete(bool batched)
    {
        var name = "MarkerPipeline." + Guid.NewGuid().ToString("N");
        var stream = await Append(batched, name, 2);
        var projector = Projector(name);
        await projector.ProcessDomainEventAsync(stream.Events[1]);
        Assert.Equal(0L, Convert.ToInt64(await fixture.Sql(
            "SELECT count(*) FROM marker_probe_receipt WHERE projector=$1", name)));
        Assert.Null(await fixture.Storage.ActorEventDb.GetEventProjectorStreamCheckpointAsync(name, stream.Id));
        await Recover(projector);
        await VerifyCompleted(stream, 2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Competing_projector_instances_cannot_apply_one_event_twice(bool batched)
    {
        var name = "MarkerPipeline." + Guid.NewGuid().ToString("N");
        var stream = await Append(batched, name, 1);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Projector(name, entered: entered, release: release);
        var pending = first.ProcessDomainEventAsync(stream.Events[0]).AsTask();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Projector(name).ProcessDomainEventAsync(stream.Events[0]);
            Assert.Equal(0L, Convert.ToInt64(await fixture.Sql(
                "SELECT count(*) FROM marker_probe_receipt WHERE projector=$1", name)));
        }
        finally { release.TrySetResult(); }
        await pending.WaitAsync(TimeSpan.FromSeconds(10));
        await Projector(name).ProcessDomainEventAsync(stream.Events[0]);
        await VerifyCompleted(stream, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Expired_owner_is_recovered_with_a_new_execution_token(bool batched)
    {
        var name = "MarkerPipeline." + Guid.NewGuid().ToString("N");
        var stream = await Append(batched, name, 1);
        var oldToken = Guid.NewGuid();
        var old = await fixture.Storage.ActorEventDb.TryClaimEventProjectorExecutionAsync(
            stream.Events[0].EventId, name, oldToken, DateTime.UtcNow.AddMinutes(-5), TimeSpan.FromSeconds(1));
        Assert.NotNull(old);
        await Recover(Projector(name));
        await VerifyCompleted(stream, 1);
        Assert.Null(await fixture.Storage.ActorEventDb.TryRenewEventProjectorExecutionAsync(
            old!.EventId, name, oldToken, old.Revision, DateTime.UtcNow, TimeSpan.FromSeconds(1)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Real_outbox_retries_publication_with_stable_identity_without_reapplying_target(bool batched)
    {
        var name = "MarkerPipeline." + Guid.NewGuid().ToString("N");
        var stream = await Append(batched, name, 1);
        var context = Substitute.For<ICommandActorContext>();
        var publications = new ConcurrentQueue<Guid>();
        var attempts = 0;
        context.SendAsync<ProbeEvent, ActorEntityId>(Arg.Any<ProbeEvent>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                publications.Enqueue(call.ArgAt<ProbeEvent>(0).Id);
                if (Interlocked.Increment(ref attempts) == 1) throw new InvalidOperationException("Injected transport failure");
                return ValueTask.CompletedTask;
            });
        var projector = new ProbeProjector(fixture, name, false, false, null, null, publication: true);
        await projector.StartAsync(context);
        try
        {
            await projector.ProcessDomainEventAsync(stream.Events[0]);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (Convert.ToInt64(await fixture.Sql(
                "SELECT count(*) FROM event_projector_outbox WHERE projectorname=$1 AND status='Published'", name)) != 1)
                await Task.Delay(10, deadline.Token);
            Assert.Equal(2, attempts);
            Assert.Single(publications.Distinct());
            Assert.Equal(2L, Convert.ToInt64(await fixture.Sql(
                "SELECT attemptcount FROM event_projector_outbox WHERE projectorname=$1", name)));
            await projector.ProcessDomainEventAsync(stream.Events[0]);
            await VerifyCompleted(stream, 1);
            Assert.Equal(1L, Convert.ToInt64(await fixture.Sql(
                "SELECT count(*) FROM event_projector_outbox WHERE projectorname=$1", name)));
            Assert.Equal(2, attempts);
        }
        finally { await projector.StopAsync(); }
    }

    sealed record Stream(long Id, string Projector, ProbeEvent[] Events);
    async Task<Stream> Append(bool batched, string projector, int count)
    {
        var streamName = "MarkerPipeline.Stream." + Guid.NewGuid().ToString("N");
        var id = await fixture.Storage.ActorEventDb.GetEventStreamIdAsync(streamName);
        var commandId = Guid.NewGuid();
        var events = Enumerable.Range(1, count).Select(value => new ProbeEvent
            { AggregateId = streamName, CommandId = commandId, Value = value, Projector = projector }).ToArray();
        var eventName = await fixture.Storage.ActorEventDb.GetEventNameIdFromDomainEventAsync(events[0]);
        var layout = EventLogSqlLayout.ForBenchmark(fixture.Provider, false, batched);
        await using var writer = new BinaryCopyEventLogAppender(fixture.Provider, true,
            new EventLogPersistenceOptions { WriteMode = EventLogWriteMode.BinaryCopy }, layout);
        var command = new ProbeCommand { CommandId = commandId, StreamId = streamName, Value = count };
        var result = await writer.AppendAsync(new(streamName, id, commandId,
            events.Select(e => new EventLogAppendEntry(eventName, e)).ToArray(), 0, DateTime.UtcNow,
            CommandAuditEnvelope.Create(command, new CommandAuditMessagePackCodec())));
        var persisted = new List<ProbeEvent>();
        foreach (var assignment in result.Assignments)
        {
            var row = await fixture.Storage.ActorEventDb.GetEventLogByEventIdAsync(assignment.EventVersion);
            persisted.Add(Assert.IsType<ProbeEvent>(row!.ToDomainEvent()));
        }
        Assert.Equal(count, Convert.ToInt32(await fixture.Sql(
            "SELECT count(*) FROM event_projector_state WHERE eventstreamid=$1 AND projectorname=$2 AND outcome='Processing'",
            id, projector)));
        return new(id, projector, persisted.ToArray());
    }

    ProbeProjector Projector(string name, bool failBefore = false, bool failAfter = false,
        TaskCompletionSource? entered = null, TaskCompletionSource? release = null)
        => new(fixture, name, failBefore, failAfter, entered, release);

    async Task<EventProjectorRecoveryResult> Recover(ProbeProjector projector)
    {
        var queue = Substitute.For<IDurableReplayQueue>();
        queue.EnqueueAsync(Arg.Any<string>(), Arg.Any<IEvent>(), Arg.Any<CancellationToken>())
            .Returns(call => projector.ProcessDomainEventAsync(call.ArgAt<IEvent>(1)));
        // Only transport delivery is replaced. Paging, deserialization, leases, transitions and checkpoints are real.
        var coordinator = new EventProjectorRecoveryCoordinator(fixture.Storage.ActorEventDb, queue,
            fixture.Blackboard, Options, NullLogger.Instance);
        return await coordinator.RecoverAsync("MarkerProbeActor", projector.ProjectorName, [typeof(ProbeEvent)])
            .WaitAsync(TimeSpan.FromSeconds(30));
    }
    async Task VerifyCompleted(Stream stream, int count)
    {
        Assert.Equal(count, Convert.ToInt32(await fixture.Sql(
            "SELECT count(*) FROM event_projector_state WHERE eventstreamid=$1 AND projectorname=$2 AND outcome='Completed' AND stage='Completed' AND executiontoken IS NULL",
            stream.Id, stream.Projector)));
        var checkpoint = await fixture.Storage.ActorEventDb.GetEventProjectorStreamCheckpointAsync(stream.Projector, stream.Id);
        Assert.Equal(count, checkpoint!.LastAppliedStreamVersion);
        Assert.Equal(count, Convert.ToInt32(await fixture.Sql(
            "SELECT mutations FROM marker_probe_current WHERE projector=$1 AND streamid=$2", stream.Projector, stream.Id)));
        Assert.Equal(count, Convert.ToInt32(await fixture.Sql(
            "SELECT lastvalue FROM marker_probe_current WHERE projector=$1 AND streamid=$2", stream.Projector, stream.Id)));
    }

    public interface IProbeActor : ICommandActor<IProbeActor> { }
    sealed class InjectedFailure : Exception { }
    sealed class ProbeProjector : BaseEventProjector<IProbeActor>
    {
        readonly MarkerProjectorFixture _fixture;
        readonly string _name;
        readonly bool _before, _after;
        readonly TaskCompletionSource? _entered, _release;
        public int AlreadyApplied { get; private set; }
        public ProbeProjector(MarkerProjectorFixture fixture, string name, bool before, bool after,
            TaskCompletionSource? entered, TaskCompletionSource? release, bool publication = false,
            IDurableReplayQueue? replayQueue = null)
            : base(replayQueue ?? Substitute.For<IDurableReplayQueue>(), fixture.Storage.ActorEventDb,
                fixture.Blackboard, NullLogger.Instance, Options with
                { TransactionalOutboxEnabled = publication, OutboxPollingInterval = TimeSpan.FromMilliseconds(25) })
        {
            _fixture = fixture; _name = name; _before = before; _after = after; _entered = entered; _release = release;
            ProjectionDescriptors = [new(typeof(ProbeEvent), EventProjectionIdempotencyStrategy.TargetReceipt,
                Apply, _ => null, (_, _) => null, publishProcessingEvent: publication,
                publishProcessingAfterApply: publication, publishTerminalEvent: false)];
        }
        public override string ActorName => "MarkerProbeActor";
        public override string ProjectorName => _name;
        public override string DurableProcessQueueName => _name + ".process";
        public override string DurableReplayQueueName => _name + ".replay";
        public override IReadOnlyCollection<Type> ProjectedEventTypes => [typeof(ProbeEvent)];
        public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors { get; }

        async ValueTask<EventProjectionApplyResult> Apply(IEvent source, ProjectionExecutionContext context)
        {
            if (_before) throw new InjectedFailure();
            _entered?.TrySetResult();
            if (_release is not null) await _release.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var domainEvent = (ProbeEvent)source;
            await using var db = new NpgsqlConnection(_fixture.Direct);
            await db.OpenAsync();
            await using var transaction = await db.BeginTransactionAsync();
            await using var receipt = new NpgsqlCommand("""
                INSERT INTO marker_probe_receipt(projector,eventid,messageid) VALUES($1,$2,$3)
                ON CONFLICT DO NOTHING RETURNING eventid
                """, db, transaction);
            receipt.Parameters.AddWithValue(_name);
            receipt.Parameters.AddWithValue(source.EventId);
            receipt.Parameters.AddWithValue(context.EffectIdentity.MessageId);
            if (await receipt.ExecuteScalarAsync() is null)
            {
                AlreadyApplied++;
                await transaction.RollbackAsync();
                return new(EventProjectionApplyOutcome.AlreadyApplied);
            }
            await using var target = new NpgsqlCommand("""
                INSERT INTO marker_probe_current(projector,streamid,lastvalue,mutations) VALUES($1,$2,$3,1)
                ON CONFLICT(projector,streamid) DO UPDATE
                SET lastvalue=excluded.lastvalue,mutations=marker_probe_current.mutations+1
                WHERE marker_probe_current.lastvalue=excluded.lastvalue-1
                RETURNING mutations
                """, db, transaction);
            target.Parameters.AddWithValue(_name);
            target.Parameters.AddWithValue(context.EventStreamId);
            target.Parameters.AddWithValue(domainEvent.Value);
            Assert.NotNull(await target.ExecuteScalarAsync());
            await transaction.CommitAsync();
            if (_after) throw new InjectedFailure();
            return new(EventProjectionApplyOutcome.Applied);
        }
    }

    public sealed record ProbeEvent : IEvent<ActorEntityId>, IRequireDurableProjection
    {
        public ActorEntityId EntityId { get; init; }
        public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
        public Guid Id { get; init; } = Guid.NewGuid();
        public long EventId { get; init; }
        public Guid CommandId { get; init; }
        public string AggregateId { get; init; } = "";
        public string EventSource { get; init; } = "MarkerProjectorPipelineTests";
        public DateTime ReceivedOn { get; init; } = DateTime.UtcNow;
        public string UserName => "benchmark";
        public string EventName => nameof(ProbeEvent);
        public EventType EventType => EventType.DomainEvent;
        public long Value { get; init; }
        public string Projector { get; init; } = "";
        public bool RequiresDurableProjection => true;
        public DurableProjectionRequirement RequiredProjection =>
            new("MarkerProbeActor", Projector, EventProjectorStageType.ApplyProjection);
    }
    public sealed record ProbeCommand : ICommand
    {
        public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
        public string CommandName => nameof(ProbeCommand);
        public BoundedContextName RouteTo => BoundedContextName.OptionTradeBoundedContext;
        public Guid CommandId { get; init; }
        public string StreamId { get; init; } = "";
        public string EventSource => "MarkerProjectorPipelineTests";
        public int ErrorCode => 1;
        public long Value { get; init; }
    }
}
