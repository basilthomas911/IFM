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
    [InlineData(ActorType.Function, ActorDeliveryType.NatsCore)]
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
        Assert.Equal(8, (int)ActorType.Function);
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

    [Fact]
    public async Task Durable_service_event_uses_started_supervisor_owned_external_producer()
    {
        var fixture = new PublisherFixture();
        var @event = CreateEvent(ActorType.Event);
        var external = new Mock<IJSActorProducer>(MockBehavior.Strict);
        fixture.Supervisor.Setup(supervisor => supervisor.ActorExists(@event.Subject.ActorId))
            .Returns(false);
        fixture.Supervisor.Setup(supervisor => supervisor.GetJSEventProducer(@event.Subject.ActorId))
            .Returns(external.Object);
        external.Setup(producer => producer.StartAsync(
                @event.Subject.ActorId,
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        external.Setup(producer => producer.SendAsync<TestEvent, ActorEntityId>(
                @event.Subject,
                @event,
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        await fixture.Publisher.SendAsync<TestEvent, ActorEntityId>(@event);

        external.VerifyAll();
        fixture.Supervisor.Verify(
            supervisor => supervisor.GetJSProducer(@event.Subject.ActorId),
            Times.Never);
        fixture.JetStream.VerifyNoOtherCalls();
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
    public async Task Notify_uses_the_publishing_actor_core_producer_without_a_notify_actor()
    {
        var fixture = new PublisherFixture();
        var @event = CreateEvent(ActorType.Notify);

        await fixture.Publisher.SendAsync<TestEvent, ActorEntityId>(@event);

        fixture.Supervisor.Verify(
            supervisor => supervisor.GetProducer(fixture.PublisherId),
            Times.Once);
        fixture.Supervisor.Verify(
            supervisor => supervisor.GetProducer(@event.Subject.ActorId),
            Times.Never);
    }

    [Fact]
    public async Task Notify_and_realtime_cache_their_distinct_core_producers()
    {
        var fixture = new PublisherFixture();
        var notify = CreateEvent(ActorType.Notify);
        var realtime = CreateEvent(ActorType.Realtime);
        var notifyProducer = new Mock<IActorProducer>(MockBehavior.Strict);
        var realtimeProducer = new Mock<IActorProducer>(MockBehavior.Strict);
        notifyProducer.Setup(producer => producer.SendAsync<TestEvent, ActorEntityId>(
                notify.Subject,
                notify,
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        realtimeProducer.Setup(producer => producer.SendAsync<TestEvent, ActorEntityId>(
                realtime.Subject,
                realtime,
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        fixture.Supervisor.Setup(supervisor => supervisor.GetProducer(fixture.PublisherId))
            .Returns(notifyProducer.Object);
        fixture.Supervisor.Setup(supervisor => supervisor.GetProducer(realtime.Subject.ActorId))
            .Returns(realtimeProducer.Object);

        await fixture.Publisher.SendAsync<TestEvent, ActorEntityId>(notify);
        await fixture.Publisher.SendAsync<TestEvent, ActorEntityId>(realtime);

        notifyProducer.VerifyAll();
        realtimeProducer.VerifyAll();
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
        internal ActorMailboxId PublisherId { get; } = new(ActorType.Event, "PublishingActor");

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
            Supervisor.Setup(supervisor => supervisor.ActorExists(It.IsAny<ActorMailboxId>()))
                .Returns(true);
            Supervisor.Setup(supervisor => supervisor.GetJSProducer(It.IsAny<ActorMailboxId>()))
                .Returns(JetStream.Object);
            Publisher = new ActorEventPublisher(Supervisor.Object, PublisherId);
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
