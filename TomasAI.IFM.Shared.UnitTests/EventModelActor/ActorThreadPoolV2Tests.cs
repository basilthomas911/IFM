using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class ActorThreadPoolV2Tests
{
    [Fact]
    public async Task SameEntity_IsFifoNonConcurrent_AndDisposesEveryMessageOnce()
    {
        const int messageCount = 500;
        var runtime = CreateRuntime(messageCount);
        await using var pool = runtime.Pool;

        for (var sequence = 0; sequence < messageCount; sequence++)
            (await runtime.Mailbox.ThreadQueues.WriteAsync(
                new TestActorMessage(sequence) { Owner = runtime.Actor })).Should().BeTrue();

        await runtime.Actor.Completed.WaitAsync(TimeSpan.FromSeconds(10));
        await runtime.Actor.DisposedCompleted.WaitAsync(TimeSpan.FromSeconds(10));

        runtime.Actor.MaximumEntityConcurrency.Should().Be(1);
        runtime.Actor.Sequences.Should().Equal(Enumerable.Range(0, messageCount));
        runtime.Actor.DisposeCounts.Should().HaveCount(messageCount);
        runtime.Actor.DisposeCounts.Values.Should().OnlyContain(count => count == 1);
    }

    [Fact]
    public async Task DifferentEntities_CanRunConcurrently()
    {
        const int messageCount = 128;
        var runtime = CreateRuntime(messageCount, handlerDelay: TimeSpan.FromMilliseconds(2));
        await using var pool = runtime.Pool;

        var writes = Enumerable.Range(0, messageCount)
            .Select(sequence => runtime.Mailbox.ThreadQueues.WriteAsync(
                new TestActorMessage(sequence, $"entity-{sequence}") { Owner = runtime.Actor }).AsTask());

        (await Task.WhenAll(writes)).Should().OnlyContain(accepted => accepted);
        await runtime.Actor.Completed.WaitAsync(TimeSpan.FromSeconds(10));

        runtime.Actor.MaximumGlobalConcurrency.Should().BeGreaterThan(1);
        runtime.Actor.MaximumEntityConcurrency.Should().Be(1);
    }

    [Fact]
    public async Task DisposeAsync_DrainsAcceptedMessagesBeforeWorkersStop()
    {
        const int messageCount = 300;
        var runtime = CreateRuntime(messageCount, handlerDelay: TimeSpan.FromMilliseconds(1));
        runtime.Pool.BeginDrainMeasurement();

        for (var sequence = 0; sequence < messageCount; sequence++)
        {
            (await runtime.Mailbox.ThreadQueues.WriteAsync(
                new TestActorMessage(sequence) { Owner = runtime.Actor })).Should().BeTrue();
        }

        await runtime.Pool.DisposeAsync();

        runtime.Actor.Sequences.Should().HaveCount(messageCount);
        runtime.Actor.DisposeCounts.Should().HaveCount(messageCount);
        runtime.Actor.DisposeCounts.Values.Should().OnlyContain(count => count == 1);
        runtime.Pool.DrainedMessageCount.Should().Be(messageCount);
    }

    static TestRuntime CreateRuntime(int expectedMessages, TimeSpan handlerDelay = default)
    {
        var mailboxId = new ActorMailboxId(ActorType.Command, "SchedulerTest");
        var container = new Mock<IContainerInstance>();
        container.Setup(instance => instance.Resolve<IActorThreadQueue>())
            .Returns(() => new ActorThreadQueueV2(64));

        var supervisor = new Mock<IActorSupervisor>();
        supervisor.SetupGet(instance => instance.Container).Returns(container.Object);

        var pool = new ActorThreadPoolV2(supervisor.Object, NullLogger.Instance);
        pool.Initialize(4);
        supervisor.SetupGet(instance => instance.ThreadPool).Returns(pool);
        supervisor.Setup(instance => instance.GetThread(It.IsAny<ActorThreadId>()))
            .Returns((ActorThreadId id) => pool.GetThread(id));
        supervisor.Setup(instance => instance.GetThreadAsync(It.IsAny<ActorThreadId>(), It.IsAny<CancellationToken>()))
            .Returns((ActorThreadId id, CancellationToken token) => pool.GetThreadAsync(id, token));

        var mailbox = new ActorMailbox(supervisor.Object, mailboxId);
        var actor = new RecordingActor(mailboxId, mailbox, expectedMessages, handlerDelay);
        IReadOnlyDictionary<ActorMailboxId, IActor> children =
            new Dictionary<ActorMailboxId, IActor> { [mailboxId] = actor };
        supervisor.SetupGet(instance => instance.Children).Returns(children);

        return new TestRuntime(pool, mailbox, actor);
    }

    sealed record TestRuntime(
        ActorThreadPoolV2 Pool,
        ActorMailbox Mailbox,
        RecordingActor Actor);

    sealed class RecordingActor(
        ActorMailboxId id,
        IActorMailbox mailbox,
        int expectedMessages,
        TimeSpan handlerDelay) : IActor
    {
        readonly ConcurrentDictionary<ActorThreadId, int> _entityConcurrency = new();
        readonly ConcurrentQueue<int> _sequences = new();
        readonly ConcurrentDictionary<int, int> _disposeCounts = new();
        readonly TaskCompletionSource _completed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly TaskCompletionSource _disposedCompleted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        int _processed;
        int _disposed;
        int _globalConcurrency;
        int _maximumGlobalConcurrency;
        int _maximumEntityConcurrency;

        public ActorMailboxId Id { get; } = id;
        public IActorMailbox Mailbox { get; } = mailbox;
        public bool IsRunning => true;
        public Task Completed => _completed.Task;
        public Task DisposedCompleted => _disposedCompleted.Task;
        public IEnumerable<int> Sequences => _sequences;
        public IReadOnlyDictionary<int, int> DisposeCounts => _disposeCounts;
        public int MaximumGlobalConcurrency => Volatile.Read(ref _maximumGlobalConcurrency);
        public int MaximumEntityConcurrency => Volatile.Read(ref _maximumEntityConcurrency);

        public ValueTask HandleMessageAsync(IActorMessage message)
            => HandleMessageAsync(message, message.Subject.ThreadId);

        public async ValueTask HandleMessageAsync(IActorMessage message, ActorThreadId threadId)
        {
            var entityConcurrency = _entityConcurrency.AddOrUpdate(threadId, 1, static (_, current) => current + 1);
            var globalConcurrency = Interlocked.Increment(ref _globalConcurrency);
            UpdateMaximum(ref _maximumEntityConcurrency, entityConcurrency);
            UpdateMaximum(ref _maximumGlobalConcurrency, globalConcurrency);
            try
            {
                if (handlerDelay > TimeSpan.Zero)
                    await Task.Delay(handlerDelay).ConfigureAwait(false);
                _sequences.Enqueue(((TestActorMessage)message).Sequence);
            }
            finally
            {
                _entityConcurrency.AddOrUpdate(threadId, 0, static (_, current) => current - 1);
                Interlocked.Decrement(ref _globalConcurrency);
                if (Interlocked.Increment(ref _processed) == expectedMessages)
                    _completed.TrySetResult();
            }
        }

        public ValueTask StartAsync(IActorSupervisor supervisor) => ValueTask.CompletedTask;
        public ValueTask StopAsync() => ValueTask.CompletedTask;

        public void RecordDispose(int sequence)
        {
            _disposeCounts.AddOrUpdate(sequence, 1, static (_, current) => current + 1);
            if (Interlocked.Increment(ref _disposed) == expectedMessages)
                _disposedCompleted.TrySetResult();
        }

        static void UpdateMaximum(ref int target, int value)
        {
            var current = Volatile.Read(ref target);
            while (value > current)
            {
                var observed = Interlocked.CompareExchange(ref target, value, current);
                if (observed == current)
                    return;
                current = observed;
            }
        }
    }

    sealed class TestActorMessage(int sequence, string entityId = "same") : IActorMessage
    {
        int _disposed;
        public int Sequence { get; } = sequence;
        public RecordingActor? Owner { get; init; }
        public ActorSubject Subject { get; } = new(ActorType.Command, "SchedulerTest", "Run", entityId);
        public ActorSubject ReplySubject { get; set; }
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => default;
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => default;
        public TQuery? AsQuery<TQuery, TResult>() where TQuery : class, IQuery<TResult> where TResult : class => default;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class => ValueTask.CompletedTask;
        public void ReleasePayload() { }
        public NatsMsg<byte[]> GetMessage() => default;
        public void Dispose()
        {
            if (Interlocked.Increment(ref _disposed) == 1)
                Owner?.RecordDispose(Sequence);
        }
    }
}
