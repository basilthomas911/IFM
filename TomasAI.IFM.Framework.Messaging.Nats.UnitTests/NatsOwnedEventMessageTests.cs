using System.Buffers;
using System.Collections.Immutable;
using FluentAssertions;
using MessagePack;
using NATS.Client.Core;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.UnitTests;

public class NatsOwnedEventMessageTests
{
    [Fact]
    public void FanoutBranches_ReleaseSharedPayloadOnlyAfterLastOwner()
    {
        var sourceEvent = CreateEvent();
        var payload = CreatePayload(sourceEvent);
        var primary = payload.CreateBranch(sourceEvent.Subject);
        var routedSubject = new ActorSubject(
            ActorType.Event,
            "RoutedEventActor",
            sourceEvent.Subject.Verb,
            sourceEvent.Subject.EntityId);
        var routed = payload.CreateBranch(routedSubject);
        primary.AdmissionSizeBytes.Should().BeGreaterThan(0);
        routed.AdmissionSizeBytes.Should().Be(primary.AdmissionSizeBytes);
        payload.ReferenceCount.Should().Be(3);
        payload.Dispose();
        payload.ReferenceCount.Should().Be(2);

        primary.AsEvent<TestEvent>().Should().BeEquivalentTo(sourceEvent);
        routed.AsEvent<TestEvent>().Should().BeEquivalentTo(sourceEvent);
        routed.Subject.Should().Be(routedSubject);

        primary.ReleasePayload();
        primary.ReleasePayload();
        payload.ReferenceCount.Should().Be(1);
        payload.IsDisposed.Should().BeFalse();

        routed.Dispose();
        payload.ReferenceCount.Should().Be(0);
        payload.IsDisposed.Should().BeTrue();
        FluentActions.Invoking(() => routed.AsEvent<TestEvent>())
            .Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task DeliveryCoordinator_AcksExactlyOnceAfterEverySuccessfulHandoff()
    {
        var acknowledgements = 0;
        var negativeAcknowledgements = 0;
        var delivery = new EventFanoutDelivery(
            3,
            () =>
            {
                Interlocked.Increment(ref acknowledgements);
                return ValueTask.CompletedTask;
            },
            () =>
            {
                Interlocked.Increment(ref negativeAcknowledgements);
                return ValueTask.CompletedTask;
            });

        await Task.WhenAll(
            delivery.CompleteHandoffAsync(true).AsTask(),
            delivery.CompleteHandoffAsync(true).AsTask(),
            delivery.CompleteHandoffAsync(true).AsTask());

        delivery.IsFinalized.Should().BeTrue();
        delivery.Remaining.Should().Be(0);
        delivery.Failures.Should().Be(0);
        acknowledgements.Should().Be(1);
        negativeAcknowledgements.Should().Be(0);
    }

    [Fact]
    public async Task DeliveryCoordinator_NaksExactlyOnceWhenAnyHandoffFails()
    {
        var acknowledgements = 0;
        var negativeAcknowledgements = 0;
        var delivery = new EventFanoutDelivery(
            3,
            () =>
            {
                Interlocked.Increment(ref acknowledgements);
                return ValueTask.CompletedTask;
            },
            () =>
            {
                Interlocked.Increment(ref negativeAcknowledgements);
                return ValueTask.CompletedTask;
            });

        await delivery.CompleteHandoffAsync(true);
        await delivery.CompleteHandoffAsync(false);
        await delivery.CompleteHandoffAsync(true);

        delivery.Failures.Should().Be(1);
        acknowledgements.Should().Be(0);
        negativeAcknowledgements.Should().Be(1);
    }

    [Fact]
    public void FanoutRoutes_DeduplicatesPrimaryAndPreservesEventIdentity()
    {
        var source = CreateEvent().Subject;
        var routedMailbox = new ActorMailboxId(ActorType.Event, "RoutedEventActor");
        var routes = ImmutableHashSet.Create(source.ActorId, routedMailbox);

        var destinations = EventFanoutRoutes.Build(source, routes, includePrimary: true);

        destinations.Should().HaveCount(2);
        destinations.Should().ContainSingle(subject => subject == source);
        destinations.Should().ContainSingle(subject =>
            subject.ActorId == routedMailbox
            && subject.Verb == source.Verb
            && subject.EntityId == source.EntityId);
    }

    [Fact]
    public void FanoutRoutes_ListenerOnlyEventHasNoActorDestinations()
    {
        var source = CreateEvent().Subject;

        var destinations = EventFanoutRoutes.Build(
            source,
            ImmutableHashSet<ActorMailboxId>.Empty,
            includePrimary: false);

        destinations.Should().BeEmpty();
    }

    [Fact]
    public void FanoutRoutes_ActorlessSourceCanStillRouteToRegisteredMailbox()
    {
        var source = CreateEvent().Subject;
        var routedMailbox = new ActorMailboxId(ActorType.Event, "RoutedEventActor");

        var destinations = EventFanoutRoutes.Build(
            source,
            ImmutableHashSet.Create(routedMailbox),
            includePrimary: false);

        destinations.Should().ContainSingle(subject =>
            subject.ActorId == routedMailbox
            && subject.Verb == source.Verb
            && subject.EntityId == source.EntityId);
    }

    static NatsSharedEventPayload CreatePayload(TestEvent @event)
    {
        var writer = new ArrayBufferWriter<byte>();
        NatsMessagePackSerializer<TestEvent>.Default.Serialize(writer, @event);
        var owner = NatsMemoryOwner<byte>.Allocate(writer.WrittenCount);
        writer.WrittenSpan.CopyTo(owner.Span);
        return new NatsSharedEventPayload(owner);
    }

    static TestEvent CreateEvent() => new()
    {
        Subject = new ActorSubject(ActorType.Event, "TestEventActor", "Recorded", "42"),
        Id = Guid.NewGuid(),
        EventId = 123,
        CommandId = Guid.NewGuid(),
        AggregateId = "42",
        EventSource = "Test",
        ReceivedOn = DateTime.UtcNow,
        Value = 42
    };

    [MessagePackObject]
    public sealed record TestEvent : IEvent
    {
        [Key(0)] public ActorSubject Subject { get; init; }
        [Key(1)] public Guid Id { get; init; }
        [Key(2)] public long EventId { get; init; }
        [Key(3)] public Guid CommandId { get; init; }
        [Key(4)] public string AggregateId { get; init; } = string.Empty;
        [Key(5)] public string EventSource { get; init; } = string.Empty;
        [Key(6)] public DateTime ReceivedOn { get; init; }
        [Key(7)] public int Value { get; init; }
        [IgnoreMember] public string UserName => "test";
        [IgnoreMember] public string EventName => nameof(TestEvent);
        [IgnoreMember] public EventType EventType => EventType.DomainEvent;
    }
}
