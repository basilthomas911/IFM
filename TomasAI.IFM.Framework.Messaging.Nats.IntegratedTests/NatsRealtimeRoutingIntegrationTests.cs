using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Net;
using NSubstitute;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.IntegratedTests;

[Trait("Category", "Integration")]
public sealed class NatsRealtimeRoutingIntegrationTests
{
    static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    readonly string _url =
        Environment.GetEnvironmentVariable("IFM_NATS_URL") ?? "nats://localhost:4222";

    [Fact]
    public async Task RealtimePublication_DeliversToPrimaryAndRegisteredRouteOnce()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var source = new ActorSubject(
            ActorType.Realtime,
            $"FuturesMarketPrice{suffix}",
            "Updated",
            "ESZ26");
        var routedMailbox = new ActorMailboxId(
            ActorType.Realtime,
            $"FuturesItiSignalRealtime{suffix}");
        var primaryReceived =
            new TaskCompletionSource<ActorSubject>(TaskCreationOptions.RunContinuationsAsynchronously);
        var routedReceived =
            new TaskCompletionSource<ActorSubject>(TaskCreationOptions.RunContinuationsAsynchronously);
        var primaryQueues = CreateAcceptingQueues(primaryReceived);
        var routedQueues = CreateAcceptingQueues(routedReceived);
        var primaryActor = CreateActor(source.ActorId, primaryQueues);
        var routedActor = CreateActor(routedMailbox, routedQueues);
        var supervisor = Substitute.For<IActorSupervisor>();
        supervisor.ActorExists(source.ActorId).Returns(true);
        supervisor.GetRealtimeRoutes(source.ActorTypeId)
            .Returns(ImmutableArray.Create(new RealtimeActorRoute(routedMailbox)));
        supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor>
        {
            [source.ActorId] = primaryActor,
            [routedMailbox] = routedActor
        });
        var consumer = new NatsActorConsumer(
            new NatsConsumerOptions
            {
                Url = _url,
                DispatcherCount = 2,
                DispatcherCapacity = 16,
                SubscriptionCapacity = 32,
                FireAndForgetTraffic = new Dictionary<ActorType, CoreNatsTrafficClass>
                {
                    [ActorType.Realtime] = CoreNatsTrafficClass.Optional
                }
            },
            Substitute.For<ILogger>());

        try
        {
            await consumer.StartAsync(
                supervisor,
                ActorType.Realtime,
                $"realtime-routing-{suffix}");
            await Task.Delay(250);
            await using var client = new NatsClient(_url);
            await client.ConnectAsync();

            await client.PublishAsync(
                source.ToString(),
                new byte[128],
                serializer: NatsDefaultSerializer<byte[]>.Default);

            var received = await Task.WhenAll(
                    primaryReceived.Task,
                    routedReceived.Task)
                .WaitAsync(TestTimeout);

            received.Should().ContainSingle(subject => subject == source);
            received.Should().ContainSingle(subject =>
                subject.ActorId == routedMailbox
                && subject.Verb == source.Verb
                && subject.EntityId == source.EntityId);
            await primaryQueues.Received(1).TryAdmitAsync(
                Arg.Any<IActorMessage>(),
                source,
                Arg.Any<CancellationToken>());
            await routedQueues.Received(1).TryAdmitAsync(
                Arg.Any<IActorMessage>(),
                Arg.Is<ActorSubject>(subject => subject.ActorId == routedMailbox),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            await consumer.StopAsync();
        }
    }

    static IActorThreadQueues CreateAcceptingQueues(
        TaskCompletionSource<ActorSubject> received)
    {
        var queues = Substitute.For<IActorThreadQueues>();
        queues.TryAdmitAsync(
                Arg.Any<IActorMessage>(),
                Arg.Any<ActorSubject>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var message = callInfo.Arg<IActorMessage>();
                var subject = callInfo.Arg<ActorSubject>();
                message.Dispose();
                received.TrySetResult(subject);
                return ValueTask.FromResult(ActorAdmissionResult.AcceptedResult);
            });
        return queues;
    }

    static IActor CreateActor(
        ActorMailboxId mailboxId,
        IActorThreadQueues queues)
    {
        var mailbox = Substitute.For<IActorMailbox>();
        mailbox.ThreadQueues.Returns(queues);
        var actor = Substitute.For<IActor>();
        actor.Id.Returns(mailboxId);
        actor.Mailbox.Returns(mailbox);
        return actor;
    }
}
