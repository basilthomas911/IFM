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

    [Theory]
    [InlineData(ActorMailboxImplementation.MpscRing)]
    [InlineData(ActorMailboxImplementation.SpscRing)]
    public async Task RingMailbox_PlugsIntoActorPoolAndPreservesFifoSingleConsumerExecution(
        ActorMailboxImplementation mailboxImplementation)
    {
        const int messageCount = 500;
        var runtime = CreateRuntime(
            messageCount,
            mailboxImplementation: mailboxImplementation);
        await using var pool = runtime.Pool;

        for (var sequence = 0; sequence < messageCount; sequence++)
        {
            (await runtime.Mailbox.ThreadQueues.WriteAsync(
                new TestActorMessage(sequence) { Owner = runtime.Actor })).Should().BeTrue();
        }
        await runtime.Actor.Completed.WaitAsync(TimeSpan.FromSeconds(10));
        await runtime.Actor.DisposedCompleted.WaitAsync(TimeSpan.FromSeconds(10));

        runtime.Actor.MaximumEntityConcurrency.Should().Be(1);
        runtime.Actor.Sequences.Should().Equal(Enumerable.Range(0, messageCount));
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

    [Fact]
    public async Task WaitForIdleAsync_WaitsForActiveMailboxAndQuietPeriod()
    {
        var runtime = CreateRuntime(expectedMessages: 1, handlerDelay: TimeSpan.FromMilliseconds(100));
        await using var pool = runtime.Pool;

        (await runtime.Mailbox.ThreadQueues.WriteAsync(
            new TestActorMessage(1) { Owner = runtime.Actor })).Should().BeTrue();

        var becameIdle = await pool.WaitForIdleAsync(
            TimeSpan.FromMilliseconds(25),
            TimeSpan.FromSeconds(2));

        becameIdle.Should().BeTrue();
        runtime.Actor.Sequences.Should().Equal(1);
    }

    [Fact]
    public async Task WaitForIdleAsync_ReturnsFalseAtHardTimeoutWhileMailboxIsActive()
    {
        var runtime = CreateRuntime(expectedMessages: 1, handlerDelay: TimeSpan.FromMilliseconds(250));
        await using var pool = runtime.Pool;

        (await runtime.Mailbox.ThreadQueues.WriteAsync(
            new TestActorMessage(1) { Owner = runtime.Actor })).Should().BeTrue();

        var becameIdle = await pool.WaitForIdleAsync(
            TimeSpan.FromMilliseconds(25),
            TimeSpan.FromMilliseconds(50));

        becameIdle.Should().BeFalse();
    }

    [Fact]
    public async Task ZeroRetainedIdleQueues_RetiresEntityQueueAfterDrain()
    {
        var runtime = CreateRuntime(expectedMessages: 1, maxRetainedIdleQueues: 0);
        await using var pool = runtime.Pool;

        (await runtime.Mailbox.ThreadQueues.WriteAsync(
            new TestActorMessage(1) { Owner = runtime.Actor })).Should().BeTrue();

        await runtime.Actor.Completed.WaitAsync(TimeSpan.FromSeconds(10));
        SpinWait.SpinUntil(
                () => runtime.Mailbox.ThreadQueues.Count == 0,
                TimeSpan.FromSeconds(2))
            .Should().BeTrue();
    }

    [Fact]
    public async Task EnforcedFullMailbox_RejectsSynchronously_AndStopReleasesAcceptedCharge()
    {
        var options = CreateEnforcedOptions(globalMessages: 4, mailboxMessages: 1);
        var controller = new ActorAdmissionController(options);
        var mailboxId = new ActorMailboxId(ActorType.Command, "AdmissionQueueTest");
        var container = new Mock<IContainerInstance>();
        container.Setup(instance => instance.Resolve<IActorThreadQueue>())
            .Returns(() => new ActorThreadQueueV2(controller, capacity: 1));
        var thread = new Mock<IActorThread>();
        var supervisor = new Mock<IActorSupervisor>();
        supervisor.SetupGet(instance => instance.Container).Returns(container.Object);
        supervisor.Setup(instance => instance.GetThreadAsync(It.IsAny<ActorThreadId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread.Object);
        var mailbox = new ActorMailbox(supervisor.Object, mailboxId, 1, controller);
        var first = new TestActorMessage(1);
        var rejectedMessage = new TestActorMessage(2);

        (await mailbox.ThreadQueues.TryAdmitAsync(first, first.Subject)).Accepted.Should().BeTrue();
        var pending = mailbox.ThreadQueues.TryAdmitAsync(rejectedMessage, rejectedMessage.Subject);

        pending.IsCompletedSuccessfully.Should().BeTrue();
        pending.Result.Reason.Should().Be(ActorAdmissionReason.MailboxLimit);
        controller.CurrentMessageCount.Should().Be(1);
        controller.CurrentByteCount.Should().Be(10);
        rejectedMessage.DisposeCount.Should().Be(0, "rejected payload ownership remains with the caller");

        mailbox.ThreadQueues.TryGetThreadQueue(first.Subject.ThreadId, out var queue).Should().BeTrue();
        queue!.Stop();
        controller.CurrentMessageCount.Should().Be(0);
        controller.CurrentByteCount.Should().Be(0);
        first.DisposeCount.Should().Be(1);
        rejectedMessage.Dispose();
    }

    [Fact]
    public async Task ConcurrentColdQueueCreation_KeepsOneQueue_AndStopsEveryLoser()
    {
        const int producerCount = 8;
        using var creationBarrier = new Barrier(producerCount);
        var created = new ConcurrentBag<TrackingQueue>();
        var container = new Mock<IContainerInstance>();
        container.Setup(instance => instance.Resolve<IActorThreadQueue>()).Returns(() =>
        {
            var queue = new TrackingQueue();
            created.Add(queue);
            creationBarrier.SignalAndWait(TimeSpan.FromSeconds(5));
            return queue;
        });
        var supervisor = new Mock<IActorSupervisor>();
        supervisor.SetupGet(instance => instance.Container).Returns(container.Object);
        var queues = new ActorThreadQueues(supervisor.Object);
        var threadId = new ActorThreadId(ActorType.Command, "ColdQueue", "1");

        var resolved = await Task.WhenAll(Enumerable.Range(0, producerCount)
            .Select(_ => Task.Run(() => queues.GetThreadQueue(threadId))));

        resolved.Distinct(ReferenceEqualityComparer.Instance).Should().ContainSingle();
        queues.Count.Should().Be(1);
        created.Should().HaveCount(producerCount);
        created.Sum(queue => queue.StopCount).Should().Be(producerCount - 1);
        created.Sum(queue => queue.StartCount).Should().Be(producerCount);
        resolved[0].Stop();
    }

    [Fact]
    public async Task EnforcedHighCardinalityBurst_CreatesNoMoreQueuesThanGlobalCapacity()
    {
        const int globalLimit = 16;
        var options = CreateEnforcedOptions(globalLimit, mailboxMessages: 1);
        var controller = new ActorAdmissionController(options);
        var mailboxId = new ActorMailboxId(ActorType.Command, "CardinalityTest");
        var container = new Mock<IContainerInstance>();
        container.Setup(instance => instance.Resolve<IActorThreadQueue>())
            .Returns(() => new ActorThreadQueueV2(controller, capacity: 1));
        var thread = new Mock<IActorThread>();
        var supervisor = new Mock<IActorSupervisor>();
        supervisor.SetupGet(instance => instance.Container).Returns(container.Object);
        supervisor.Setup(instance => instance.GetThreadAsync(It.IsAny<ActorThreadId>(), It.IsAny<CancellationToken>()))
            .Returns((ActorThreadId _, CancellationToken _) => ValueTask.FromResult(thread.Object));
        var mailbox = new ActorMailbox(supervisor.Object, mailboxId, globalLimit, controller);

        var results = await Task.WhenAll(Enumerable.Range(0, 128).Select(index =>
        {
            var message = new TestActorMessage(index, $"entity-{index}");
            return mailbox.ThreadQueues.TryAdmitAsync(message, message.Subject).AsTask();
        }));

        results.Count(result => result.Accepted).Should().Be(globalLimit);
        results.Where(result => !result.Accepted).Should()
            .OnlyContain(result => result.Reason == ActorAdmissionReason.GlobalMessageLimit);
        mailbox.ThreadQueues.Count.Should().Be(globalLimit);
        controller.CurrentMessageCount.Should().Be(globalLimit);

        foreach (var index in Enumerable.Range(0, globalLimit))
        {
            var threadId = new ActorThreadId(ActorType.Command, "SchedulerTest", $"entity-{index}");
            mailbox.ThreadQueues.TryGetThreadQueue(threadId, out var queue).Should().BeTrue();
            queue!.Stop();
        }
        controller.CurrentMessageCount.Should().Be(0);
        controller.CurrentByteCount.Should().Be(0);
    }

    [Fact]
    public void RetiredQueueRetry_ReusesOneReservation_AndStoppingReturnsDistinctReason()
    {
        var controller = new ActorAdmissionController(CreateEnforcedOptions(2, mailboxMessages: 1));
        var message = new TestActorMessage(1);
        controller.TryReserve(message, ActorType.Command, out var charge).Accepted.Should().BeTrue();
        using var retired = new ActorThreadQueueV2(controller, capacity: 1);
        retired.SetId(message.Subject.ThreadId);
        retired.Start();
        var retiredQueue = (IScheduledActorThreadQueue)retired;
        retiredQueue.TryRetire().Should().BeTrue();

        var retry = retiredQueue.TryWriteReserved(message, charge, default);

        retry.Reason.Should().Be(ActorAdmissionReason.MailboxRetired);
        controller.CurrentMessageCount.Should().Be(1);
        using var replacement = new ActorThreadQueueV2(controller, capacity: 1);
        replacement.SetId(message.Subject.ThreadId);
        replacement.Start();
        var replacementQueue = (IScheduledActorThreadQueue)replacement;
        replacementQueue.TryWriteReserved(message, charge, default).Accepted.Should().BeTrue();
        replacementQueue.TryRead(out var dequeued).Should().BeTrue();
        dequeued!.Dispose();
        controller.CurrentMessageCount.Should().Be(0);

        replacement.Stop();
        var stoppingMessage = new TestActorMessage(2);
        controller.TryReserve(stoppingMessage, ActorType.Command, out var stoppingCharge).Accepted.Should().BeTrue();
        replacementQueue.TryWriteReserved(stoppingMessage, stoppingCharge, default).Reason
            .Should().Be(ActorAdmissionReason.Stopping);
        controller.Release(stoppingCharge);
        stoppingMessage.Dispose();
        controller.CurrentMessageCount.Should().Be(0);
    }

    static TestRuntime CreateRuntime(
        int expectedMessages,
        TimeSpan handlerDelay = default,
        int maxRetainedIdleQueues = ActorAdmissionOptions.ExistingRetainedIdleMailboxesPerActor,
        ActorMailboxImplementation mailboxImplementation = ActorMailboxImplementation.Channel)
    {
        var mailboxId = new ActorMailboxId(ActorType.Command, "SchedulerTest");
        var container = new Mock<IContainerInstance>();
        container.Setup(instance => instance.Resolve<IActorThreadQueue>())
            .Returns(() => mailboxImplementation switch
            {
                ActorMailboxImplementation.Channel => new ActorThreadQueueV2(64),
                ActorMailboxImplementation.MpscRing => new ActorThreadQueueMpscRing(64),
                ActorMailboxImplementation.SpscRing => new ActorThreadQueueSpscRing(64),
                _ => throw new ArgumentOutOfRangeException(nameof(mailboxImplementation))
            });

        var supervisor = new Mock<IActorSupervisor>();
        supervisor.SetupGet(instance => instance.Container).Returns(container.Object);

        var pool = new ActorThreadPoolV2(supervisor.Object, NullLogger.Instance);
        pool.Initialize(4);
        supervisor.SetupGet(instance => instance.ThreadPool).Returns(pool);
        supervisor.Setup(instance => instance.GetThread(It.IsAny<ActorThreadId>()))
            .Returns((ActorThreadId id) => pool.GetThread(id));
        supervisor.Setup(instance => instance.GetThreadAsync(It.IsAny<ActorThreadId>(), It.IsAny<CancellationToken>()))
            .Returns((ActorThreadId id, CancellationToken token) => pool.GetThreadAsync(id, token));

        var mailbox = new ActorMailbox(supervisor.Object, mailboxId, maxRetainedIdleQueues);
        var actor = new RecordingActor(mailboxId, mailbox, expectedMessages, handlerDelay);
        IReadOnlyDictionary<ActorMailboxId, IActor> children =
            new Dictionary<ActorMailboxId, IActor> { [mailboxId] = actor };
        supervisor.SetupGet(instance => instance.Children).Returns(children);

        return new TestRuntime(pool, mailbox, actor);
    }

    static ActorAdmissionOptions CreateEnforcedOptions(int globalMessages, int mailboxMessages)
        => new()
        {
            Mode = ActorAdmissionMode.Enforce,
            GlobalMessageLimit = globalMessages,
            GlobalByteLimit = globalMessages * 10,
            MaximumPayloadBytes = 10,
            DefaultActorTypeMessageLimit = globalMessages,
            DefaultActorTypeByteLimit = globalMessages * 10,
            DefaultMailboxMessageLimit = mailboxMessages
        };

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
        public int AdmissionSizeBytes => 10;
        public int DisposeCount => Volatile.Read(ref _disposed);
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

    sealed class TrackingQueue : IActorThreadQueue
    {
        int _startCount;
        int _stopCount;
        public int StartCount => Volatile.Read(ref _startCount);
        public int StopCount => Volatile.Read(ref _stopCount);
        public ActorThreadId Id { get; private set; }
        public int Count => 0;
        public IActorThreadQueue SetId(ActorThreadId id)
        {
            Id = id;
            return this;
        }
        public IAsyncEnumerable<IActorMessage> ReadAllAsync(CancellationToken cancellationToken = default)
            => EmptyAsync();
        public IEnumerable<IActorMessage> ReadAll(CancellationToken cancellationToken = default) => [];
        public bool Write(IActorMessage message, CancellationToken cancellationToken = default) => false;
        public ValueTask EnqueueAsync(IActorMessage message, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
        public void Start() => Interlocked.Increment(ref _startCount);
        public void Stop() => Interlocked.Increment(ref _stopCount);

        static async IAsyncEnumerable<IActorMessage> EmptyAsync()
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
