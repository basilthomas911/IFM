using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

/// <summary>Real local scheduler + real bar actor + durable journal; only the downstream command transport is substituted.</summary>
[MemoryDiagnoser]
public class BarSchedulerBenchmarks
{
    Harness before = null!, after = null!;
    [Params(1, 64)] public int Count { get; set; }
    [GlobalSetup] public async Task Setup() { before = await Harness.Create(false); after = await Harness.Create(true); }
    [Benchmark(Baseline = true)] public Task PerMessageBefore() => before.Run(Count);
    [Benchmark] public Task BatchedAfter() => after.Run(Count);
    [GlobalCleanup] public async Task Cleanup() { await before.DisposeAsync(); await after.DisposeAsync(); }

    sealed class Harness : IAsyncDisposable
    {
        public ActorThreadPoolV2 Pool = null!;
        public ActorMailbox Mailbox = null!;
        public RealtimeMailboxSafetyFixture.Handler Handler = null!;
        public FuturesTradeSessionBarCheckpointStore Store = null!;
        ActorThreadId thread;
        long ordinal = 2;
        public static async Task<Harness> Create(bool batched)
        {
            var value = new Harness { Store = new(Path.Combine(Path.GetTempPath(), "IFM-bar-scheduler-" + Guid.NewGuid().ToString("N"))) };
            value.Handler = new(value.Store);
            var supervisor = Substitute.For<IActorSupervisor>();
            var container = Substitute.For<IContainerInstance>();
            container.Resolve<IActorThreadQueue>().Returns(_ => new ActorThreadQueueV2(128));
            supervisor.Container.Returns(container);
            supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorProducer>());
            value.Pool = new(supervisor, NullLogger.Instance); value.Pool.Initialize(2);
            supervisor.ThreadPool.Returns(value.Pool);
            supervisor.GetThread(Arg.Any<ActorThreadId>()).Returns(call => value.Pool.GetThread(call.Arg<ActorThreadId>()));
            supervisor.GetThreadAsync(Arg.Any<ActorThreadId>(), Arg.Any<CancellationToken>())
                .Returns(call => value.Pool.GetThreadAsync(call.Arg<ActorThreadId>(), call.Arg<CancellationToken>()));
            value.Mailbox = new(supervisor, value.Handler.Actor.Id);
            supervisor.CreateMailbox(Arg.Any<ActorMailboxId>()).Returns(value.Mailbox);
            IActor scheduled = batched ? value.Handler.Actor : new PerMessageActor(value.Handler.Actor);
            supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor> { [scheduled.Id] = scheduled });
            await value.Handler.Actor.StartAsync(supervisor);
            await value.Handler.Receive(RealtimeMailboxSafetyFixture.Trade(1)); await value.Handler.Receive(RealtimeMailboxSafetyFixture.Trade(2));
            var id = value.Handler.Context.Accumulators.Resolve("ESU6", RealtimeMailboxSafetyFixture.ValueDate);
            value.thread = new(scheduled.Id.ActorType, scheduled.Id.Name, id.Format());
            return value;
        }
        public async Task Run(int count)
        {
            var messages = new RealtimeMailboxSafetyFixture.BatchMessage[count];
            var remaining = count;
            var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            for (var n = 0; n < count; n++)
            {
                messages[n] = new(RealtimeMailboxSafetyFixture.Trade(++ordinal), thread);
                messages[n].OnDisposed = () => { if (Interlocked.Decrement(ref remaining) == 0) completed.TrySetResult(); };
                if (!await Mailbox.ThreadQueues.WriteAsync(messages[n])) throw new IOException("Scheduler benchmark admission failed.");
            }
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(15));
            if (messages.Any(message => message.Disposals != 1 || message.Outcomes.Count != 1 || !message.Outcomes[0]))
                throw new InvalidDataException("Scheduler benchmark delivery failed.");
        }
        public async ValueTask DisposeAsync() { await Pool.DisposeAsync(); await Handler.Actor.StopAsync(); Store.Dispose(); }
    }
    // Frozen per-message dispatch reference; it deliberately does not opt in to IBatchedRealtimeActor.
    sealed class PerMessageActor(IActor inner) : IActor
    {
        public ActorMailboxId Id => inner.Id;
        public IActorMailbox Mailbox => inner.Mailbox;
        public bool IsRunning => inner.IsRunning;
        public ValueTask StartAsync(IActorSupervisor supervisor) => inner.StartAsync(supervisor);
        public ValueTask StopAsync() => inner.StopAsync();
        public ValueTask HandleMessageAsync(IActorMessage message) => inner.HandleMessageAsync(message);
        public ValueTask HandleMessageAsync(IActorMessage message, ActorThreadId thread, CancellationToken token) => inner.HandleMessageAsync(message, thread, token);
    }
}
