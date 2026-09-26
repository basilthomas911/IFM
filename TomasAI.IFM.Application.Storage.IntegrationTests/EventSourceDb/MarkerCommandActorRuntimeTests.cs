using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using NATS.Net;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed partial class MarkerProjectorPipelineTests
{
    [Xunit.Theory]
    [Xunit.InlineData(false, false, true)]
    [Xunit.InlineData(true, false, true)]
    [Xunit.InlineData(false, true, true)]
    [Xunit.InlineData(true, true, true)]
    [Xunit.InlineData(false, false, false)]
    [Xunit.InlineData(true, false, false)]
    [Xunit.InlineData(false, true, false)]
    [Xunit.InlineData(true, true, false)]
    public async Task Command_actor_routes_atomic_appends_and_recovers_after_runtime_recreation(bool batched, bool omitFirstHandoff, bool ownedPayloads)
    {
        const string url = "nats://127.0.0.1:24223";
        Xunit.Assert.Equal(url, Environment.GetEnvironmentVariable("IFM_MARKER_TEST_NATS_URL"));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var name = "MarkerActor_" + Guid.NewGuid().ToString("N");
        var actorId = new ActorMailboxId(ActorType.Command, name);
        var commands = Enumerable.Range(0, 4).Select(i => new RuntimeCommand
        {
            CommandId = Guid.NewGuid(), Subject = new(ActorType.Command, name, "Append", "s" + i),
            StreamId = name + ".s" + i, Projector = name, Count = 8
        }).ToArray();
        await using var client = new NatsClient(url);
        async Task<ServiceResult<GuidResult>> Send(RuntimeCommand command)
        {
            var reply = await client.RequestAsync<RuntimeCommand, ServiceResult<GuidResult>>(
                command.Subject.ToString(), command,
                requestSerializer: NatsMessagePackSerializer<RuntimeCommand>.Default,
                replySerializer: NatsMessagePackSerializer<ServiceResult<GuidResult>>.Default,
                cancellationToken: deadline.Token);
            return reply.Data!;
        }

        for (var lifetime = 0; lifetime < 2; lifetime++)
        {
            // New context also discards the in-memory command deduplication cache.
            await using var db = fixture.Storage.CreateBenchmarkActorEventDb(batched);
            var container = new RuntimeContainer(db);
            var logs = new RuntimeLog();
            await using var supervisor = new ActorSupervisor(container, logs);
            await using var queue = new NatsJSDurableReplayQueue(new NatsJetStreamConsumerOptions { Url = url });
            var projector = new ProbeProjector(fixture, name, false, false, null, null, replayQueue: queue);
            var context = new RuntimeContext(supervisor, actorId);
            var actor = new RuntimeActor(context, db, queue, projector, lifetime == 0 && omitFirstHandoff);
            supervisor.AddActor(actor);
            supervisor.AddProducer(actorId, new NatsActorProducer(new NatsProducerOptions { Url = url }, NullLogger.Instance));
            supervisor.AddConsumer(ActorType.Command, new NatsActorConsumer(new NatsConsumerOptions
            { Url = url, DispatcherCount = 2, UseOwnedCommandPayloads = ownedPayloads }, logs));
            try
            {
                await supervisor.StartAsync(actorId, deadline.Token);
                Xunit.Assert.True(actor.IsRunning);
                Xunit.Assert.Equal(lifetime == 1 && omitFirstHandoff ? 32 : 0, projector.Readiness.RecoveryEventsDiscovered);
                await supervisor.StartConsumersAsync(deadline.Token);
                supervisor.SetReadiness(true);
                var replies = await Task.WhenAll(commands.Select(Send));
                Xunit.Assert.All(replies, r => Xunit.Assert.True(r.Success, r.ErrorMessage));
                // Same command over actual NATS request/reply must not append again.
                var duplicates = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Send(commands[0])));
                Xunit.Assert.All(duplicates, r => Xunit.Assert.True(r.Success, r.ErrorMessage));
                var collision = await Send(commands[0] with { Count = 9 });
                Xunit.Assert.False(collision.Success);
                Xunit.Assert.Contains("different command payload", collision.ErrorMessage);
                if (lifetime == 1)
                {
                    var continuation = commands.Select(c => c with { CommandId = Guid.NewGuid(), ExpectedVersion = 8 }).ToArray();
                    var continued = await Task.WhenAll(continuation.Select(Send));
                    Xunit.Assert.All(continued, r => Xunit.Assert.True(r.Success, r.ErrorMessage));
                    Xunit.Assert.Equal(4, actor.ReloadedStreams);
                }
                var stale = await Send(commands[0] with { CommandId = Guid.NewGuid() });
                Xunit.Assert.False(stale.Success);
                var expected = lifetime == 0 ? 32 : 64;
                if (!(lifetime == 0 && omitFirstHandoff))
                    while (Convert.ToInt32(await fixture.Sql(
                        "SELECT count(*) FROM event_projector_state WHERE projectorname=$1 AND outcome='Completed'", name)) != expected)
                        await Task.Delay(10, deadline.Token);
                Xunit.Assert.Equal(expected, Convert.ToInt32(await fixture.Sql(
                    "SELECT count(*) FROM event_projector_state WHERE projectorname=$1", name)));
                Xunit.Assert.Equal(lifetime == 0 ? 4 : 8, Convert.ToInt32(await fixture.Sql(
                    "SELECT count(*) FROM command_log WHERE streamid LIKE $1", name + ".%")));
                var broker = client.CreateJetStreamContext();
                while (true)
                {
                    var process = await broker.GetConsumerAsync($"IFM_{name}_PROCESS", $"{name}-process-worker", deadline.Token);
                    var replay = await broker.GetConsumerAsync($"IFM_{name}_REPLAY", $"{name}-replay-worker", deadline.Token);
                    if (process.Info.NumPending == 0 && process.Info.NumAckPending == 0 &&
                        replay.Info.NumPending == 0 && replay.Info.NumAckPending == 0) break;
                    await Task.Delay(10, deadline.Token);
                }
            }
            finally { await supervisor.ShutdownAsync(); }
        }
        foreach (var command in commands)
        {
            var id = await fixture.Storage.ActorEventDb.GetEventStreamIdAsync(command.StreamId);
            await VerifyCompleted(new Stream(id, name, []), 16);
        }
    }

    sealed class RuntimeContainer(EventSourceActorDbContext db) : IContainerInstance
    {
        public T Resolve<T>() where T : class => typeof(T) == typeof(ICommandAuditLogger) ? (T)(object)db
            : typeof(T) == typeof(IActorThreadQueue) ? (T)(object)new ActorThreadQueueV2()
            : throw new InvalidOperationException("Unregistered test dependency: " + typeof(T).FullName);
    }

    sealed class RuntimeLog : ILogger<ActorSupervisor>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Information;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> format)
        { if (IsEnabled(level)) Console.WriteLine(format(state, error) + " " + error); }
    }

    sealed class RuntimeContext(IActorSupervisor supervisor, ActorMailboxId actorId)
        : CommandActorContext(supervisor, actorId), ICommandActorContext<RuntimeActor>;

    public sealed record RuntimeCommand : ICommand
    {
        public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
        public string CommandName => "Append";
        public BoundedContextName RouteTo => BoundedContextName.OptionTradeBoundedContext;
        public Guid CommandId { get; init; }
        public string StreamId { get; init; } = "";
        public string EventSource => "MarkerActorQualification";
        public int ErrorCode => 9911;
        public string Projector { get; init; } = "";
        public int Count { get; init; }
        public long ExpectedVersion { get; init; }
    }

    sealed class RuntimeState : IActorState<RuntimeState>
    {
        public ActorThreadId Id { get; set; }
        public long Version { get; set; }
    }

    sealed class RuntimeActor(RuntimeContext context, EventSourceActorDbContext db,
        NatsJSDurableReplayQueue queue, ProbeProjector projector, bool omitHandoff)
        : BaseEventSourceCommandActor<RuntimeActor>(context, NullLogger.Instance)
    {
        readonly ConcurrentDictionary<string, RuntimeState> states = new();
        int reloadedStreams;
        public int ReloadedStreams => reloadedStreams;
        protected override bool AuditIsCommittedWithState(ICommand command) => true;
        protected override bool IsCommittedDuplicateException(ICommand command, Exception error) => error is CommandAuditDuplicateException;
        protected override ICommand ParseMessage(ICommandActorContext<RuntimeActor> ctx, IActorMessage message)
        {
            if (message.Subject.ActorId != Id || message.Subject.Verb != "Append") throw new InvalidOperationException("Unexpected route");
            return message.AsCommand<RuntimeCommand>()!;
        }
        protected override async ValueTask OnStartup(ICommandActorContext<RuntimeActor> ctx) => await projector.StartAsync(ctx);
        protected override async ValueTask OnShutdown(ICommandActorContext<RuntimeActor> ctx) => await projector.StopAsync();
        protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<RuntimeActor> ctx, ActorThreadId thread, ICommand command)
        {
            if (states.TryGetValue(command.StreamId, out var state)) return state;
            var id = await db.GetEventStreamIdAsync(command.StreamId);
            var rows = await db.LoadActorEventStreamAsync<RuntimeState>(id);
            state = new RuntimeState { Id = thread, Version = rows.Count == 0 ? 0 : rows.Max(r => r.StreamVersion) };
            if (state.Version > 0) Interlocked.Increment(ref reloadedStreams);
            states[command.StreamId] = state;
            return state;
        }
        protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<RuntimeActor> ctx, IActorState state, ICommand command)
            => ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new GuidResult(command.CommandId)));
        protected override async ValueTask OnSaveStateAsync(ICommandActorContext<RuntimeActor> ctx, ActorThreadId thread, IActorState state, ICommand command)
        {
            var request = (RuntimeCommand)command;
            var events = new DomainEventCollection(Enumerable.Range(1, request.Count).Select(i => (IEvent)new ProbeEvent
            {
                AggregateId = command.StreamId, CommandId = command.CommandId, Projector = request.Projector,
                Value = request.ExpectedVersion + i
            }));
            var saved = await db.SaveCommandEventsAtomicallyAsync(command, events, request.ExpectedVersion);
            ((RuntimeState)state).Version = request.ExpectedVersion + saved.Count;
            // Deliberately omit this handoff in the recovery case to simulate its loss after durable commit.
            if (!omitHandoff)
                foreach (var source in saved) await queue.EnqueueAsync(request.Projector, source);
        }
        protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<RuntimeActor> ctx, ActorThreadId thread, ICommand command, Exception error)
            => ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(9911, error.Message));
    }
}
