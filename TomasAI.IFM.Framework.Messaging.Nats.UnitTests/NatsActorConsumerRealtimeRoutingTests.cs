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
            ImmutableArray.Create(
                new RealtimeActorRoute(Source.ActorId),
                new RealtimeActorRoute(routedMailbox)));

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
    public void Realtime_RouteCanOverrideOnlyDestinationSchedulingEntity()
    {
        var routedMailbox = new ActorMailboxId(
            ActorType.Realtime,
            "FuturesTradeSessionBarSignal");
        var supervisor = Substitute.For<IActorSupervisor>();
        supervisor.ActorExists(Source.ActorId).Returns(true);
        supervisor.GetRealtimeRoutes(Source.ActorTypeId).Returns(
            ImmutableArray.Create(new RealtimeActorRoute(routedMailbox, _ => "2026-09-02")));

        var destinations = NatsActorConsumer.BuildPubSubDestinations(
            supervisor,
            ActorType.Realtime,
            Source);

        destinations.Should().HaveCount(2);
        destinations.Should().ContainSingle(subject => subject == Source);
        destinations.Should().ContainSingle(subject =>
            subject.ActorId == routedMailbox
            && subject.Verb == Source.Verb
            && subject.EntityId == "2026-09-02");
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
    public void LegacyCommand_RemainsDirectAndDoesNotConsultRealtimeRoutes()
    {
        var command = new ActorSubject(
            ActorType.Command,
            "MarketDataFeedCommand",
            "Start",
            "42");
        var supervisor = Substitute.For<IActorSupervisor>();

        var destinations = NatsActorConsumer.BuildPubSubDestinations(
            supervisor,
            ActorType.Command,
            command);

        destinations.Should().ContainSingle().Which.Should().Be(command);
        supervisor.DidNotReceive().ActorExists(Arg.Any<ActorMailboxId>());
        supervisor.DidNotReceive().GetRealtimeRoutes(Arg.Any<ActorTypeId>());
    }

    [Fact]
    public void Consumer_BindsToOneBackendActorType()
    {
        var bound = NatsActorConsumer.BindActorType(
            ActorType.Unknown,
            ActorType.Command);

        NatsActorConsumer.BindActorType(bound, ActorType.Command)
            .Should().Be(ActorType.Command);
        var bindOtherType = () => NatsActorConsumer.BindActorType(
            bound,
            ActorType.Query);
        bindOtherType.Should().Throw<InvalidOperationException>()
            .WithMessage("*already bound*Command*Query*");
    }

    [Fact]
    public void Consumer_RejectsNotifyActorType()
    {
        var bindNotify = () => NatsActorConsumer.BindActorType(
            ActorType.Unknown,
            ActorType.Notify);

        bindNotify.Should().Throw<InvalidOperationException>()
            .WithMessage("*reserved for UI, console, and external NATS event listeners*");
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
