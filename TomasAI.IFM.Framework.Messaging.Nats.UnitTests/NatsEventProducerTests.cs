using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.UnitTests;

public class NatsEventProducerTests
{
    [Fact]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
    public void RouteCache_PreservesInheritedConstantsAndConcurrentResolution()
    {
        Parallel.For(0, 256, _ =>
        {
            NatsEventProducer.GetPublicStaticRouteValue(typeof(InheritedRoute), "Actor").Should().Be("InheritedActor");
            NatsEventProducer.GetPublicStaticRouteValue(typeof(InheritedRoute), "Verb").Should().Be("Changed");
        });
    }

    [Fact]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
    public void RouteCache_ReadsMutableFieldsAndGettersOnEveryCall()
    {
        foreach (var value in new[] { "First", "Second" })
        {
            MutableRoute.Actor = value;
            NatsEventProducer.GetPublicStaticRouteValue(typeof(MutableRoute), "Actor").Should().Be(value);
            NatsEventProducer.GetPublicStaticRouteValue(typeof(MutableRoute), "Verb").Should().Be(value);
        }
        NatsEventProducer.GetPublicStaticRouteValue(typeof(MutableRoute), "Missing").Should().BeNull();
        NatsEventProducer.GetPublicStaticRouteValue(typeof(MutableRoute), "NullConstant").Should().BeNull();
        NatsEventProducer.GetPublicStaticRouteValue(typeof(MutableRoute), "Number").Should().BeNull();
    }

    [Fact]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
    public void RouteCache_DoesNotSuppressGetterExceptionsAfterWarmup()
    {
        ThrowingRoute.Throws = false;
        NatsEventProducer.GetPublicStaticRouteValue(typeof(ThrowingRoute), "Actor").Should().Be("Actor");
        ThrowingRoute.Throws = true;
        Action read = () => NatsEventProducer.GetPublicStaticRouteValue(typeof(ThrowingRoute), "Actor");
        read.Should().Throw<System.Reflection.TargetInvocationException>().WithInnerException<InvalidOperationException>();
    }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="xUnit discovers public test classes and fixtures.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1034",Justification="These public nested types are concrete serializer test fixtures.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1052",Justification="Preserve test fixture construction and discovery conventions.")]

    public class ConstantRoute { public const string Actor = "InheritedActor"; public const string Verb = "Changed"; }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="xUnit discovers public test classes and fixtures.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1034",Justification="These public nested types are concrete serializer test fixtures.")]
    public sealed class InheritedRoute : ConstantRoute;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1034",Justification="These public nested types are concrete serializer test fixtures.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="xUnit discovers public test classes and fixtures.")]
    public static class MutableRoute
    {
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA2211",Justification="Preserve the existing mutable configuration or test fixture contract.")]
        public static string Actor = "First";
        public static string Verb => Actor;
        public const string? NullConstant = null;
        public const int Number = 1;
    }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1034",Justification="These public nested types are concrete serializer test fixtures.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="xUnit discovers public test classes and fixtures.")]
    public static class ThrowingRoute
    {
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA2211",Justification="Preserve the existing mutable configuration or test fixture contract.")]
        public static bool Throws;
        public static string Actor => Throws ? throw new InvalidOperationException("getter") : "Actor";
    }

    [Fact]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
    public void PrepareEvent_MissingSubject_DerivesNatsRouteAndInitializesDeliveryMetadata()
    {
        var @event = new RoutedTestEvent
        {
            CommandId = Guid.NewGuid(),
            EventSource = "test"
        };

        var subject = NatsEventProducer.PrepareEvent("test", 42, @event);

        subject.Should().Be(new ActorSubject(ActorType.Event, RoutedTestEvent.Actor, RoutedTestEvent.Verb, "42"));
        @event.Subject.Should().Be(subject);
        @event.Id.Should().NotBeEmpty();
        @event.ReceivedOn.Should().NotBe(default);
    }

    [Fact]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
    public void PrepareEvent_ValidSubject_PreservesExistingRouteAndIdentity()
    {
        var originalSubject = new ActorSubject(ActorType.Event, "ExistingActor", "ExistingVerb", "entity-7");
        var originalId = Guid.NewGuid();
        var originalReceivedOn = DateTime.UtcNow.AddMinutes(-1);
        var @event = new RoutedTestEvent
        {
            Subject = originalSubject,
            Id = originalId,
            CommandId = Guid.NewGuid(),
            EventSource = "test",
            ReceivedOn = originalReceivedOn
        };

        var subject = NatsEventProducer.PrepareEvent("test", 99, @event);

        subject.Should().Be(originalSubject);
        @event.Id.Should().Be(originalId);
        @event.ReceivedOn.Should().Be(originalReceivedOn);
    }

    [Fact]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
    public void ResolveSubject_EventWithoutActorRoute_ThrowsInvalidOperationException()
    {
        var @event = new UnroutedTestEvent
        {
            CommandId = Guid.NewGuid(),
            EventSource = "test"
        };

        Action act = () => NatsEventProducer.ResolveSubject(@event, "entity");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*does not expose a valid public static Actor route*");
    }

    [Fact]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA2000",Justification="The test deliberately exercises transferred resource ownership and explicit disposal assertions.")]
    public async Task Core_producer_rejects_durable_event_before_connecting()
    {
        var producer = new NatsActorProducer(
            new NatsProducerOptions(),
            NullLogger<NatsActorProducer>.Instance);
        var @event = CreateEntityEvent(ActorType.Event);

        var act = async () => await (producer.SendAsync<EntityTestEvent, ActorEntityId>(
            @event.Subject,
            @event)).ConfigureAwait(true);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*requires delivery 'NatsJetStream'*");
    }

    [Fact]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA2000",Justification="The test deliberately exercises transferred resource ownership and explicit disposal assertions.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
    public async Task JetStream_producer_rejects_nondurable_event_before_connecting()
    {
        var producer = new NatsJetStreamActorProducer(
            new NatsJetStreamProducerOptions(),
            NullLogger<NatsJetStreamActorProducer>.Instance);
        var @event = CreateEntityEvent(ActorType.Realtime);

        var act = async () => await (producer.SendAsync<EntityTestEvent, ActorEntityId>(
            @event.Subject,
            @event)).ConfigureAwait(true);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*requires delivery 'NatsCore'*");
    }

    static EntityTestEvent CreateEntityEvent(ActorType actorType) => new()
    {
        Subject = new ActorSubject(actorType, "TestEvent", "Updated", "1"),
        Id = Guid.NewGuid(),
        CommandId = Guid.NewGuid(),
        EntityId = ActorEntityId.Default,
        AggregateId = "1",
        EventSource = "unit-test",
        ReceivedOn = DateTime.UtcNow
    };
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1852",Justification="Preserve test fixture construction and discovery conventions.")]

    private record RoutedTestEvent : TestEvent
    {
        public const string Actor = "TestEvent";
        public const string Verb = "Updated";
    }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1852",Justification="Preserve test fixture construction and discovery conventions.")]

    private record UnroutedTestEvent : TestEvent;

    private sealed record EntityTestEvent : TestEvent, IEvent<ActorEntityId>
    {
        public ActorEntityId EntityId { get; init; }
    }

    private abstract record TestEvent : IEvent
    {
        public ActorSubject Subject { get; init; }
        public Guid Id { get; init; }
        public long EventId { get; init; }
        public Guid CommandId { get; init; }
        public string AggregateId { get; init; } = string.Empty;
        public string EventSource { get; init; } = string.Empty;
        public DateTime ReceivedOn { get; init; }
        public string UserName => "UnitTest";
        public string EventName => GetType().Name;
        public EventType EventType => EventType.DomainEvent;
    }
}
