using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class ActorThreadQueueSpscRingTests
{
    [Fact]
    public async Task EnqueueAsync_WhenFull_WaitsWithoutBlockingTheCaller()
    {
        using var queue = CreateQueue(capacity: 2);
        var scheduled = (IScheduledActorThreadQueue)queue;
        await queue.EnqueueAsync(new TestActorMessage(1));
        await queue.EnqueueAsync(new TestActorMessage(2));

        var pending = queue.EnqueueAsync(new TestActorMessage(3));

        pending.IsCompleted.Should().BeFalse();
        scheduled.TryRead(out var first).Should().BeTrue();
        ((TestActorMessage)first!).Sequence.Should().Be(1);
        await pending.AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        Drain(scheduled).Select(message => message.Sequence).Should().Equal(2, 3);
    }

    [Fact]
    public async Task SingleProducerAndConsumer_ProcessEveryMessageOnceInOrder()
    {
        const int total = 10_000;
        using var queue = CreateQueue(capacity: 256);
        var received = new bool[total];
        var duplicates = 0;
        var lastSequence = -1;

        var consumer = Task.Run(async () =>
        {
            var count = 0;
            await foreach (var actorMessage in queue.ReadAllAsync())
            {
                var message = (TestActorMessage)actorMessage;
                if (received[message.Sequence])
                    duplicates++;
                received[message.Sequence] = true;
                message.Sequence.Should().BeGreaterThan(lastSequence);
                lastSequence = message.Sequence;
                if (++count == total)
                    return;
            }
        });

        var producer = Task.Run(async () =>
        {
            for (var index = 0; index < total; index++)
                await queue.EnqueueAsync(new TestActorMessage(index));
        });

        await producer.WaitAsync(TimeSpan.FromSeconds(10));
        await consumer.WaitAsync(TimeSpan.FromSeconds(10));
        duplicates.Should().Be(0);
        received.Should().OnlyContain(value => value);
        queue.Count.Should().Be(0);
    }

    [Fact]
    public void CompleteDrain_ClosesTheEnqueueVersusIdleRace()
    {
        using var queue = CreateQueue(capacity: 8);
        var scheduled = (IScheduledActorThreadQueue)queue;
        var first = new TestActorMessage(1);
        var raced = new TestActorMessage(2);

        scheduled.TryWrite(first, default).Should().BeTrue();
        scheduled.TrySchedule().Should().BeTrue();
        scheduled.TryRead(out _).Should().BeTrue();
        scheduled.TryWrite(raced, default).Should().BeTrue();
        scheduled.TrySchedule().Should().BeFalse();

        scheduled.CompleteDrain().Should().BeTrue();
        scheduled.TryRead(out var read).Should().BeTrue();
        read.Should().BeSameAs(raced);
        scheduled.CompleteDrain().Should().BeFalse();
    }

    [Fact]
    public void Enforce_RejectsAtRingCapacityAndReleasesAllAccountingAfterDrain()
    {
        var controller = new ActorAdmissionController(CreateEnforcedOptions(mailboxCapacity: 2));
        using var queue = CreateQueue(2, controller);
        var scheduled = (IScheduledActorThreadQueue)queue;

        queue.Write(new TestActorMessage(1)).Should().BeTrue();
        queue.Write(new TestActorMessage(2)).Should().BeTrue();
        queue.Write(new TestActorMessage(3)).Should().BeFalse();
        controller.CurrentMessageCount.Should().Be(2);

        Drain(scheduled).Should().HaveCount(2);
        controller.CurrentMessageCount.Should().Be(0);
        controller.CurrentByteCount.Should().Be(0);
    }

    [Fact]
    public async Task CanceledFullWrite_ReleasesItsAdmissionReservation()
    {
        var controller = new ActorAdmissionController(CreateEnforcedOptions(mailboxCapacity: 2, enforce: false));
        using var queue = CreateQueue(2, controller);
        await queue.EnqueueAsync(new TestActorMessage(1));
        await queue.EnqueueAsync(new TestActorMessage(2));
        using var cancellation = new CancellationTokenSource();

        var pending = queue.EnqueueAsync(new TestActorMessage(3), cancellation.Token);
        cancellation.Cancel();

        await pending.AsTask().Invoking(task => task).Should().ThrowAsync<OperationCanceledException>();
        controller.CurrentMessageCount.Should().Be(2);
        Drain((IScheduledActorThreadQueue)queue).Should().HaveCount(2);
        controller.CurrentMessageCount.Should().Be(0);
    }

    [Fact]
    public void Stop_DrainsAndDisposesOwnedMessages()
    {
        var controller = new ActorAdmissionController(CreateEnforcedOptions(mailboxCapacity: 2));
        var queue = CreateQueue(2, controller);
        var first = new TestActorMessage(1);
        var second = new TestActorMessage(2);
        queue.Write(first).Should().BeTrue();
        queue.Write(second).Should().BeTrue();

        queue.Stop();

        first.DisposeCount.Should().Be(1);
        second.DisposeCount.Should().Be(1);
        controller.CurrentMessageCount.Should().Be(0);
        controller.CurrentByteCount.Should().Be(0);
        queue.Count.Should().Be(0);
        queue.Stop();
    }

    [Fact]
    public void Options_RequirePowerOfTwoOnlyForSpscRing()
    {
        var ringOptions = new ActorAdmissionOptions
        {
            MailboxImplementation = ActorMailboxImplementation.SpscRing,
            DefaultMailboxMessageLimit = 3
        };
        var channelOptions = new ActorAdmissionOptions
        {
            MailboxImplementation = ActorMailboxImplementation.Channel,
            DefaultMailboxMessageLimit = 3
        };

        ringOptions.Invoking(options => options.Validate()).Should().Throw<InvalidOperationException>()
            .WithMessage("*power of two*SpscRing*");
        channelOptions.Invoking(options => options.Validate()).Should().NotThrow();
    }

    static ActorThreadQueueSpscRing CreateQueue(
        int capacity,
        ActorAdmissionController? controller = null)
    {
        var queue = new ActorThreadQueueSpscRing(
            controller ?? ActorAdmissionController.Disabled,
            capacity);
        queue.SetId(new ActorThreadId(ActorType.Command, "SpscRingTest", "1"));
        queue.Start();
        return queue;
    }

    static List<TestActorMessage> Drain(IScheduledActorThreadQueue scheduled)
    {
        var messages = new List<TestActorMessage>();
        while (scheduled.TryRead(out var message))
            messages.Add((TestActorMessage)message!);
        return messages;
    }

    static ActorAdmissionOptions CreateEnforcedOptions(int mailboxCapacity, bool enforce = true)
        => new()
        {
            Mode = enforce ? ActorAdmissionMode.Enforce : ActorAdmissionMode.ObserveOnly,
            MailboxImplementation = ActorMailboxImplementation.SpscRing,
            GlobalMessageLimit = 16,
            GlobalByteLimit = 16_384,
            MaximumPayloadBytes = 1_024,
            DefaultActorTypeMessageLimit = 16,
            DefaultActorTypeByteLimit = 16_384,
            DefaultMailboxMessageLimit = mailboxCapacity,
            JetStreamNakDelayMilliseconds = 25,
            OverloadErrorCode = -429
        };

    sealed class TestActorMessage(
        int sequence,
        int producer = 0,
        int producerSequence = 0) : IActorMessage
    {
        int _disposeCount;
        public int Sequence { get; } = sequence;
        public int Producer { get; } = producer;
        public int ProducerSequence { get; } = producerSequence;
        public int DisposeCount => Volatile.Read(ref _disposeCount);
        public int AdmissionSizeBytes => 128;
        public ActorSubject Subject { get; } = new(ActorType.Command, "SpscRingTest", "Run", sequence.ToString());
        public ActorSubject ReplySubject { get; set; }
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => default;
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => default;
        public TQuery? AsQuery<TQuery, TResult>()
            where TQuery : class, IQuery<TResult>
            where TResult : class => default;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class => ValueTask.CompletedTask;
        public void ReleasePayload() { }
        public NatsMsg<byte[]> GetMessage() => default;
        public void Dispose() => Interlocked.Increment(ref _disposeCount);
    }
}
