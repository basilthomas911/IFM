using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class ActorDeliveryMappingTests
{
    [Theory]
    [InlineData(ActorType.Unknown, ActorDeliveryType.Unknown)]
    [InlineData(ActorType.Command, ActorDeliveryType.NatsCore)]
    [InlineData(ActorType.Query, ActorDeliveryType.NatsCore)]
    [InlineData(ActorType.Notify, ActorDeliveryType.NatsCore)]
    [InlineData(ActorType.Realtime, ActorDeliveryType.NatsCore)]
    [InlineData(ActorType.Event, ActorDeliveryType.NatsJetStream)]
    public void Actor_type_has_one_delivery_transport(
        ActorType actorType,
        ActorDeliveryType expected) =>
        Assert.Equal(expected, actorType.GetDeliveryType());

    [Fact]
    public void Removed_actor_types_are_not_defined_and_wire_values_are_stable()
    {
        Assert.Equal(0, (int)ActorType.Unknown);
        Assert.Equal(2, (int)ActorType.Command);
        Assert.Equal(3, (int)ActorType.Event);
        Assert.Equal(4, (int)ActorType.Query);
        Assert.Equal(5, (int)ActorType.Notify);
        Assert.Equal(7, (int)ActorType.Realtime);
        Assert.DoesNotContain("Supervisor", Enum.GetNames<ActorType>());
        Assert.DoesNotContain("UI", Enum.GetNames<ActorType>());
    }

    [Fact]
    public async Task Durable_event_uses_only_jetstream()
    {
        var fixture = new PublisherFixture();
        var @event = CreateEvent(ActorType.Event);

        await fixture.Publisher.SendAsync<TestEvent, ActorEntityId>(@event);

        fixture.JetStream.Verify(
            producer => producer.SendAsync<TestEvent, ActorEntityId>(
                @event.Subject,
                @event,
                It.IsAny<CancellationToken>()),
            Times.Once);
        fixture.Core.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(ActorType.Notify)]
    [InlineData(ActorType.Realtime)]
    public async Task Non_durable_event_uses_only_core_nats(ActorType actorType)
    {
        var fixture = new PublisherFixture();
        var @event = CreateEvent(actorType);

        await fixture.Publisher.SendAsync<TestEvent, ActorEntityId>(@event);

        fixture.Core.Verify(
            producer => producer.SendAsync<TestEvent, ActorEntityId>(
                @event.Subject,
                @event,
                It.IsAny<CancellationToken>()),
            Times.Once);
        fixture.JetStream.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Unknown_event_is_rejected_before_resolving_a_producer()
    {
        var fixture = new PublisherFixture();
        var @event = CreateEvent(ActorType.Unknown);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await fixture.Publisher.SendAsync<TestEvent, ActorEntityId>(@event));

        fixture.Supervisor.VerifyNoOtherCalls();
    }

    static TestEvent CreateEvent(ActorType actorType) => new()
    {
        Subject = new ActorSubject(actorType, "TestActor", "Recorded", "1"),
        Id = Guid.NewGuid(),
        CommandId = Guid.NewGuid(),
        EntityId = ActorEntityId.Default,
        AggregateId = "1",
        EventSource = "unit-test",
        ReceivedOn = DateTime.UtcNow
    };

    sealed class PublisherFixture
    {
        internal Mock<IActorSupervisor> Supervisor { get; } = new(MockBehavior.Strict);
        internal Mock<IActorProducer> Core { get; } = new(MockBehavior.Strict);
        internal Mock<IJSActorProducer> JetStream { get; } = new(MockBehavior.Strict);
        internal ActorEventPublisher Publisher { get; }

        internal PublisherFixture()
        {
            Core.Setup(producer => producer.SendAsync<TestEvent, ActorEntityId>(
                    It.IsAny<ActorSubject>(),
                    It.IsAny<TestEvent>(),
                    It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);
            JetStream.Setup(producer => producer.SendAsync<TestEvent, ActorEntityId>(
                    It.IsAny<ActorSubject>(),
                    It.IsAny<TestEvent>(),
                    It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);
            Supervisor.Setup(supervisor => supervisor.GetProducer(It.IsAny<ActorMailboxId>()))
                .Returns(Core.Object);
            Supervisor.Setup(supervisor => supervisor.GetJSProducer(It.IsAny<ActorMailboxId>()))
                .Returns(JetStream.Object);
            Publisher = new ActorEventPublisher(Supervisor.Object);
        }
    }

    sealed record TestEvent : IEvent<ActorEntityId>
    {
        public ActorSubject Subject { get; init; }
        public Guid Id { get; init; }
        public long EventId { get; init; }
        public Guid CommandId { get; init; }
        public ActorEntityId EntityId { get; init; }
        public string AggregateId { get; init; } = string.Empty;
        public string EventSource { get; init; } = string.Empty;
        public DateTime ReceivedOn { get; init; }
        public string UserName => "UnitTest";
        public string EventName => nameof(TestEvent);
        public EventType EventType => EventType.DomainEvent;
    }
}
