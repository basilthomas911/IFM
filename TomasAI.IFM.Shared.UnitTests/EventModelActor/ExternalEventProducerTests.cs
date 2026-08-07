using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class ExternalEventProducerTests
{
    [Fact]
    public async Task Concurrent_get_or_create_returns_one_supervisor_owned_producer()
    {
        var producer = new FakeProducer();
        var container = new Mock<IContainerInstance>();
        container.Setup(x => x.Resolve<IJSActorProducer>()).Returns(producer);
        await using var supervisor = new ActorSupervisor(container.Object, NullLogger<ActorSupervisor>.Instance);
        var id = new ActorMailboxId(ActorType.Event, "TickAggregationSourceEventActor");

        var producers = await Task.WhenAll(Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => supervisor.GetJSEventProducer(id))));

        Assert.All(producers, value => Assert.Same(producer, value));
        container.Verify(x => x.Resolve<IJSActorProducer>(), Times.Once);
        Assert.Throws<InvalidOperationException>(() => supervisor.RemoveJSProducer(id));
        await supervisor.ShutdownAsync();
        Assert.Equal(1, producer.StopCount);
    }

    private sealed class FakeProducer : IJSActorProducer
    {
        public int StopCount;
        public bool IsRunning { get; private set; }
        public ValueTask SendAsync<TCommand, TEntityId>(ActorSubject subject, TCommand command, TEntityId entityId)
            where TCommand : class, ICommand<TEntityId> where TEntityId : IActorEntityId => ValueTask.CompletedTask;
        public ValueTask SendAsync<TEvent, TEntityId>(ActorSubject subject, TEvent @event)
            where TEvent : class, IEvent<TEntityId> where TEntityId : IActorEntityId => ValueTask.CompletedTask;
        public ValueTask StartAsync(ActorMailboxId mailboxId) { IsRunning = true; return ValueTask.CompletedTask; }
        public ValueTask StopAsync() { IsRunning = false; Interlocked.Increment(ref StopCount); return ValueTask.CompletedTask; }
    }
}
