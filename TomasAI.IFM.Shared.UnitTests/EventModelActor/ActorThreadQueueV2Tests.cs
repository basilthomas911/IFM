using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class ActorThreadQueueV2Tests
{
    [Fact]
    public async Task EnqueueAsync_WhenFull_WaitsWithoutBlockingTheCaller()
    {
        using var queue = CreateQueue(capacity: 1);
        var first = new TestActorMessage(1);
        var second = new TestActorMessage(2);

        await queue.EnqueueAsync(first);
        var pending = queue.EnqueueAsync(second);

        pending.IsCompleted.Should().BeFalse();
        var scheduled = (IScheduledActorThreadQueue)queue;
        scheduled.TryRead(out var read).Should().BeTrue();
        read.Should().BeSameAs(first);
        await pending.AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        scheduled.TryRead(out read).Should().BeTrue();
        read.Should().BeSameAs(second);
    }

    [Fact]
    public void CompleteDrain_ClosesTheEnqueueVersusIdleRace()
    {
        using var queue = CreateQueue();
        var scheduled = (IScheduledActorThreadQueue)queue;
        var first = new TestActorMessage(1);
        var raced = new TestActorMessage(2);

        scheduled.TryWrite(first, default).Should().BeTrue();
        scheduled.TrySchedule().Should().BeTrue();
        scheduled.TrySchedule().Should().BeFalse();
        scheduled.TryRead(out var read).Should().BeTrue();
        read.Should().BeSameAs(first);

        // The producer observes that the mailbox is already scheduled, exactly as it would while a worker drains.
        scheduled.TryWrite(raced, default).Should().BeTrue();
        scheduled.TrySchedule().Should().BeFalse();

        scheduled.CompleteDrain().Should().BeTrue();
        scheduled.TryRead(out read).Should().BeTrue();
        read.Should().BeSameAs(raced);
        scheduled.CompleteDrain().Should().BeFalse();
    }

    [Fact]
    public async Task MultipleProducers_AreAcceptedWithoutLoss()
    {
        const int producerCount = 8;
        const int messagesPerProducer = 500;
        using var queue = CreateQueue(capacity: 512);
        var received = new HashSet<int>();

        var consumer = Task.Run(async () =>
        {
            await foreach (var message in queue.ReadAllAsync())
            {
                received.Add(((TestActorMessage)message).Sequence);
                if (received.Count == producerCount * messagesPerProducer)
                    break;
            }
        });

        var producers = Enumerable.Range(0, producerCount).Select(producer => Task.Run(async () =>
        {
            for (var index = 0; index < messagesPerProducer; index++)
                await queue.EnqueueAsync(new TestActorMessage((producer * messagesPerProducer) + index));
        }));

        await Task.WhenAll(producers);
        await consumer.WaitAsync(TimeSpan.FromSeconds(5));
        received.Should().HaveCount(producerCount * messagesPerProducer);
    }

    static ActorThreadQueueV2 CreateQueue(int capacity = 32)
    {
        var queue = new ActorThreadQueueV2(capacity);
        queue.SetId(new ActorThreadId(ActorType.Command, "Test", "1"));
        queue.Start();
        return queue;
    }

    sealed class TestActorMessage(int sequence) : IActorMessage
    {
        public int Sequence { get; } = sequence;
        public ActorSubject Subject { get; } = new(ActorType.Command, "Test", "Run", sequence.ToString());
        public ActorSubject ReplySubject { get; set; }
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => default;
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => default;
        public TQuery? AsQuery<TQuery, TResult>() where TQuery : class, IQuery<TResult> where TResult : class => default;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class => ValueTask.CompletedTask;
        public void ReleasePayload() { }
        public NatsMsg<byte[]> GetMessage() => default;
        public void Dispose() { }
    }
}
