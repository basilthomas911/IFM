using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Command;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Command.Actor;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Command.EventProjector;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Command.State;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.MarketData.IntegrationTests;

[Collection("DownloadLog runtime")]
[Trait("Category", "Integration")]
public sealed class DownloadLogRecoveryTests(MarketDataFixture fixture) : IClassFixture<MarketDataFixture>
{
    static readonly EventProjectorReliabilityOptions Options = new()
    {
        BoundedRecoveryEnabled = true, FencedExecutionEnabled = true, TransactionalOutboxEnabled = true,
        InitialReplayDelay = TimeSpan.FromMilliseconds(100), ClaimLeaseDuration = TimeSpan.FromSeconds(2),
        OutboxPollingInterval = TimeSpan.FromMilliseconds(100), MaximumReplayAttempts = 3
    };

    [Fact]
    public async Task Event_insert_rolls_back_when_required_projection_marker_cannot_be_created()
    {
        var e = new InvalidProjectionEvent();
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.ActorEventSourceDb.SaveEventsAsync(
            "DownloadLogAtomicVerification." + Guid.NewGuid().ToString("N"), e.CommandId, new DomainEventCollection([e]), 0, CancellationToken.None));
        Assert.True(e.EventId > 0);
        Assert.Null(await fixture.ActorEventSourceDb.GetEventLogByEventIdAsync(e.EventId));
    }

    public sealed class InvalidProjectionEvent : IEvent, IRequireDurableProjection
    {
        public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
        public Guid Id { get; init; } = Guid.NewGuid();
        public long EventId { get; init; }
        public Guid CommandId { get; init; } = Guid.NewGuid();
        public string AggregateId { get; init; } = "atomic-verification";
        public string EventSource { get; init; } = "DownloadLogCommandActor";
        public DateTime ReceivedOn { get; init; } = DateTime.UtcNow;
        public string UserName => "test";
        public string EventName => nameof(InvalidProjectionEvent);
        public EventType EventType => EventType.DomainEvent;
        public DurableProjectionRequirement RequiredProjection => new("DownloadLogCommandActor", "DownloadLogEventProjector", EventProjectorStageType.Completed);
    }

    [Theory] [InlineData("before-enqueue")] [InlineData("storage-outage")] [InlineData("after-upsert")]
    [InlineData("notification-outage")] [InlineData("exhausted")]
    public async Task Committed_outcome_survives_restart_and_repeat_application_without_reimport(string fault)
    {
        var settings = new DbConnectionSettings().Add("EventSourceActorDbConnection", "Host=localhost;Port=5432;Database=event-source-test-db", "System.Data.Postgres");
        var schema = new EventSourceSchemaDb(settings, NullLogger<DbProvider>.Instance);
        await schema.CreateAsync(schema.ManagedObjects);
        var outcome = new MarketDataDownloadOutcome
        {
            Dataset = MarketDataDownloadDataset.TreasuryCurve, Scope = "US", ValueDate = new(8994, 9, 5),
            ImportCommandId = Guid.NewGuid(), SourceTerminalEventId = Guid.NewGuid(),
            RequestedAtUtc = new(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc), StartedAtUtc = new(2026, 9, 5, 12, 0, 1, DateTimeKind.Utc),
            FinishedAtUtc = new(2026, 9, 5, 12, 0, 2, DateTimeKind.Utc), Status = MarketDataDownloadStatus.Completed,
            DownloadedRecordCount = 1, PersistedRecordCount = 1, ElapsedMilliseconds = 1000
        };
        var command = new InsertMarketDataDownloadLogCommand(outcome); var state = new DownloadLogCommandState(); command.Execute(state);
        var events = await fixture.ActorEventSourceDb.SaveEventsAsync(command.StreamId, command.CommandId, state.Events, 0, CancellationToken.None);
        var inserted = Assert.IsType<MarketDataDownloadLogInsertedEvent>(Assert.Single(events));
        Assert.NotNull(await fixture.ActorEventSourceDb.GetEventProjectorExecutionStateAsync(inserted.EventId, "DownloadLogEventProjector", CancellationToken.None));
        var partition = new MarketDataDownloadPartition(outcome.Dataset, "FMP", "US", outcome.ValueDate);
        Assert.False((await fixture.MarketDataDb.GetMarketDataDownloadStatusAsync(partition, outcome.ImportCommandId)).CompletionConfirmed);
        var scope = "downloadlog_recovery_" + Guid.NewGuid().ToString("N");
        var calls = 0; var failing = fault != "before-enqueue";
        var attempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var notificationAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var target = Substitute.For<IMarketDataDbContext>();
        target.InsertMarketDataDownloadLogAsync(Arg.Any<MarketDataDownloadOutcome>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                Interlocked.Increment(ref calls);
                if (fault is "storage-outage" or "exhausted" && Volatile.Read(ref failing))
                { attempted.TrySetResult(); throw new InvalidOperationException("Injected target unavailable"); }
                await fixture.MarketDataDb.InsertMarketDataDownloadLogAsync(call.Arg<MarketDataDownloadOutcome>(), call.Arg<Guid>(), call.Arg<string>(), call.Arg<CancellationToken>());
                attempted.TrySetResult();
                if (fault == "after-upsert" && Volatile.Read(ref failing)) throw new InvalidOperationException("Injected loss before target checkpoint");
            });
        if (fault != "before-enqueue")
        {
            await using var firstQueue = new ScopedQueue(scope);
            var firstContext = Context(firstQueue, target); var first = new DownloadLogEventProjector(firstContext, Options);
            if (fault == "notification-outage")
                firstContext.SendAsync<MarketDataDownloadLogInsertedCompleteEvent, DownloadLogId>(Arg.Any<MarketDataDownloadLogInsertedCompleteEvent>(), Arg.Any<CancellationToken>())
                    .Returns(_ => { notificationAttempted.TrySetResult(); return ValueTask.FromException(new InvalidOperationException("Injected notification transport outage")); });
            await first.StartAsync(firstContext);
            try { await attempted.Task.WaitAsync(TimeSpan.FromSeconds(20)); }
            catch (TimeoutException)
            {
                var stalled = await fixture.ActorEventSourceDb.GetEventProjectorExecutionStateAsync(inserted.EventId, first.ProjectorName, CancellationToken.None);
                await first.StopAsync();
                throw new InvalidOperationException("Recovery stalled: " + System.Text.Json.JsonSerializer.Serialize(stalled));
            }
            if (fault == "storage-outage") Assert.False((await fixture.MarketDataDb.GetMarketDataDownloadStatusAsync(partition, outcome.ImportCommandId)).CompletionConfirmed);
            if (fault == "notification-outage")
            {
                await notificationAttempted.Task.WaitAsync(TimeSpan.FromSeconds(20));
                Assert.True((await fixture.MarketDataDb.GetMarketDataDownloadStatusAsync(partition, outcome.ImportCommandId)).CompletionConfirmed);
            }
            if (fault == "exhausted")
            {
                using var exhaustedTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                while (true)
                {
                    var execution = await fixture.ActorEventSourceDb.GetEventProjectorExecutionStateAsync(inserted.EventId, first.ProjectorName, exhaustedTimeout.Token);
                    if (execution?.Outcome == EventProjectorOutcomeType.Failed) break;
                    await Task.Delay(100, exhaustedTimeout.Token);
                }
                await first.StopAsync();
                Assert.False((await fixture.MarketDataDb.GetMarketDataDownloadStatusAsync(partition, outcome.ImportCommandId)).CompletionConfirmed);
                Assert.Equal(MarketDataDownloadStatus.Completed, inserted.Outcome.Status);
                Assert.InRange(calls, 2, 5);
                return;
            }
            await first.StopAsync();
        }
        Volatile.Write(ref failing, false);
        await using var restartedQueue = new ScopedQueue(scope);
        var restartedContext = Context(restartedQueue, target); var restarted = new DownloadLogEventProjector(restartedContext, Options);
        var notificationRecovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        restartedContext.SendAsync<MarketDataDownloadLogInsertedCompleteEvent, DownloadLogId>(Arg.Any<MarketDataDownloadLogInsertedCompleteEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ => { notificationRecovered.TrySetResult(); return ValueTask.CompletedTask; });
        try
        {
            // No enqueue here: recovery must discover the PostgreSQL event by itself.
            await restarted.StartAsync(restartedContext);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (true)
            {
                var execution = await fixture.ActorEventSourceDb.GetEventProjectorExecutionStateAsync(inserted.EventId, restarted.ProjectorName, timeout.Token);
                if (execution?.Outcome == EventProjectorOutcomeType.Completed) break;
                await Task.Delay(100, timeout.Token);
            }
            var exact = await fixture.MarketDataDb.GetMarketDataDownloadLogAsync(partition, new(outcome.RequestedAtUtc, outcome.ImportCommandId));
            Assert.Equal(outcome, exact.Attempt!.Outcome);
            Assert.Equal(command.PayloadSha256, exact.Attempt.PayloadSha256);
            Assert.True(calls >= 1);
            if (fault == "notification-outage")
            {
                await notificationRecovered.Task.WaitAsync(TimeSpan.FromSeconds(20));
                Assert.Equal(1, calls);
            }
        }
        finally { await restarted.StopAsync(); }
    }

    IDownloadLogCommandContext Context(IDurableReplayQueue queue, IMarketDataDbContext target)
    {
        var ctx = Substitute.For<IDownloadLogCommandContext>(); var factory = Substitute.For<IDbContextFactory>(); factory.MarketDataDb.Returns(target);
        ctx.DbFactory.Returns(factory); ctx.DurableReplayQueue.Returns(queue); ctx.DbEventSource.Returns(fixture.ActorEventSourceDb);
        ctx.BlackboardService.Returns(fixture.BlackboardService); ctx.Logger.Returns(NullLogger<DownloadLogCommandActor>.Instance);
        ctx.ActorId.Returns(new ActorMailboxId(ActorType.Command, DownloadLogCommandActor.ActorName));
        return ctx;
    }

    // Isolate test transport resources while exercising the unchanged production projector identity and engine.
    sealed class ScopedQueue(string scope) : IDurableReplayQueue, IAsyncDisposable
    {
        readonly NatsJSDurableReplayQueue inner = new(new NatsJetStreamConsumerOptions { Url = Environment.GetEnvironmentVariable("IFM_DOWNLOADLOG_TEST_NATS_URL") ?? "nats://127.0.0.1:14222" });
        public Task PrepareAsync(string name, TimeSpan delay, CancellationToken ct = default) => inner.PrepareAsync(scope, delay, ct);
        public Task StartAsync(string name, TimeSpan delay, CancellationToken ct = default) => inner.StartAsync(scope, delay, ct);
        public Task StopAsync(string name, CancellationToken ct = default) => inner.StopAsync(scope, ct);
        public ValueTask EnqueueAsync(string name, IEvent e, CancellationToken ct = default) => inner.EnqueueAsync(scope, e, ct);
        public Task DequeueAsync(string name, Func<IEvent, Task<EventProjectorDeliveryResult>> handler, CancellationToken ct = default) => inner.DequeueAsync(scope, handler, ct);
        public void SetMaxAttemptsReachedAction(string name, Func<IEvent, Task<EventProjectorDeliveryResult>> handler, bool overwrite = true) => inner.SetMaxAttemptsReachedAction(scope, handler, overwrite);
        public void SetMaxReplayAttemps(string name, int count, bool overwrite = true) => inner.SetMaxReplayAttemps(scope, count, overwrite);
        public int GetMaxReplayAttemps(string name) => inner.GetMaxReplayAttemps(scope);
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
