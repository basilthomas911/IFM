using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class ActorThreadQueuesRetentionTests
{
    [Fact]
    public void ReplacingRetiredMailbox_DoesNotInflateRetentionCount()
    {
        using var fixture = new Mailboxes(1);
        var id = Id(0);
        for (var i = 0; i < 50; i++)
        {
            var retired = fixture.Queues.GetThreadQueue(id);
            ((IScheduledActorThreadQueue)retired).TryRetire().Should().BeTrue();
            var replacement = fixture.Queues.GetThreadQueue(id);
            replacement.Should().NotBeSameAs(retired);
            fixture.Queues.ReleaseThreadQueue(id);
            fixture.Queues.TryGetThreadQueue(id, out var retained).Should().BeTrue();
            retained.Should().BeSameAs(replacement);
            fixture.Queues.Count.Should().Be(1);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    public void ConcurrentCreateAndRelease_ConvergesWithoutCounterDrift(int retentionLimit)
    {
        using var fixture = new Mailboxes(retentionLimit);
        Parallel.For(0, 4096, i =>
        {
            // Reuse identities as well as creating them so replacement/removal races are exercised.
            var id = Id(i % 128);
            fixture.Queues.GetThreadQueue(id);
            fixture.Queues.ReleaseThreadQueue(id);
        });
        foreach (var i in Enumerable.Range(0, 128))
            fixture.Queues.ReleaseThreadQueue(Id(i));

        fixture.Queues.Count.Should().BeLessThanOrEqualTo(retentionLimit);
        var count = (int)typeof(ActorThreadQueues)
            .GetField("_publishedOrPendingQueues", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(fixture.Queues)!;
        count.Should().Be(fixture.Queues.Count, "all publications and removals have completed");
    }

    static ActorThreadId Id(int i) => new(ActorType.Realtime, "FuturesItiSignal", i.ToString());

    sealed class Mailboxes : IDisposable
    {
        readonly ConcurrentBag<ActorThreadQueueV2> created = new();
        public ActorThreadQueues Queues { get; }

        public Mailboxes(int retentionLimit)
        {
            var container = new Mock<IContainerInstance>();
            container.Setup(instance => instance.Resolve<IActorThreadQueue>()).Returns(() =>
            {
                var queue = new ActorThreadQueueV2(8);
                created.Add(queue);
                return queue;
            });
            var supervisor = new Mock<IActorSupervisor>();
            supervisor.SetupGet(instance => instance.Container).Returns(container.Object);
            Queues = new ActorThreadQueues(supervisor.Object, retentionLimit);
        }

        public void Dispose()
        {
            foreach (var queue in created)
                queue.Dispose();
        }
    }
}
