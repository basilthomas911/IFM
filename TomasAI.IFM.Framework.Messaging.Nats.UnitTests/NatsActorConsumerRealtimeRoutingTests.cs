using System.Collections.Immutable;
using FluentAssertions;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class NatsActorConsumerRealtimeRoutingTests
{
    static readonly ActorSubject Source =
        new(ActorType.Realtime, "FuturesMarketPrice", "Updated", "ESZ26");

    [Fact]
    public void Realtime_IncludesPrimaryAndDeduplicatedRegisteredRoutes()
    {
        var routedMailbox =
            new ActorMailboxId(ActorType.Realtime, "FuturesItiSignalRealtime");
        var supervisor = Substitute.For<IActorSupervisor>();
        supervisor.ActorExists(Source.ActorId).Returns(true);
        supervisor.GetRealtimeRoutes(Source.ActorTypeId).Returns(
            ImmutableHashSet.Create(Source.ActorId, routedMailbox));

        var destinations = NatsActorConsumer.BuildPubSubDestinations(
            supervisor,
            ActorType.Realtime,
            Source);

        destinations.Should().HaveCount(2);
        destinations.Should().ContainSingle(subject => subject == Source);
        destinations.Should().ContainSingle(subject =>
            subject.ActorId == routedMailbox
            && subject.Verb == Source.Verb
            && subject.EntityId == Source.EntityId);
    }

    [Fact]
    public void Realtime_RejectsRoutingWhenPrimaryActorIsMissing()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        supervisor.ActorExists(Source.ActorId).Returns(false);

        var destinations = NatsActorConsumer.BuildPubSubDestinations(
            supervisor,
            ActorType.Realtime,
            Source);

        destinations.Should().BeEmpty();
        supervisor.DidNotReceive().GetRealtimeRoutes(Arg.Any<ActorTypeId>());
    }

    [Fact]
    public void Notify_RemainsDirectAndDoesNotConsultRealtimeRoutes()
    {
        var notify = new ActorSubject(
            ActorType.Notify,
            "StatusConsole",
            "Updated",
            "42");
        var supervisor = Substitute.For<IActorSupervisor>();

        var destinations = NatsActorConsumer.BuildPubSubDestinations(
            supervisor,
            ActorType.Notify,
            notify);

        destinations.Should().ContainSingle().Which.Should().Be(notify);
        supervisor.DidNotReceive().ActorExists(Arg.Any<ActorMailboxId>());
        supervisor.DidNotReceive().GetRealtimeRoutes(Arg.Any<ActorTypeId>());
    }

    [Fact]
    public void RoutedMessage_ExposesDestinationSubjectAndOriginalPayload()
    {
        byte[] payload = [1, 2, 3, 4];
        var sourceMessage = new NatsMsg<byte[]>(
            Source.ToString(),
            null,
            default,
            default,
            payload,
            default,
            default);
        var destination = new ActorSubject(
            ActorType.Realtime,
            "FuturesItiSignalRealtime",
            Source.Verb,
            Source.EntityId);

        var message = new NatsActorMessage(sourceMessage, destination);

        message.Subject.Should().Be(destination);
        message.GetMessage().Subject.Should().Be(Source.ToString());
        message.GetMessage().Data.Should().BeSameAs(payload);
    }
}
