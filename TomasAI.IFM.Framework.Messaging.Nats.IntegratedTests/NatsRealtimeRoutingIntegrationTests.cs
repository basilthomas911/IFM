using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MessagePack;
using NATS.Client.Core;
using NATS.Net;
using NSubstitute;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

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
        var generation = Guid.NewGuid();
        var primaryReceived =
            new TaskCompletionSource<ReceivedRealtime>(TaskCreationOptions.RunContinuationsAsynchronously);
        var routedReceived =
            new TaskCompletionSource<ReceivedRealtime>(TaskCreationOptions.RunContinuationsAsynchronously);
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
            var producer = new NatsActorProducer(new NatsProducerOptions { Url = _url },
                Substitute.For<ILogger>());
            await producer.StartAsync(source.ActorId);
            try
            {
                await producer.SendAsync<TaggedRealtimeEvent, ActorEntityId>(source,
                    new TaggedRealtimeEvent
                    {
                        Subject = source,
                        Id = Guid.NewGuid(),
                        SourceDataset = "GLBX.MDP3",
                        SourceGenerationId = generation
                    });
            }
            finally { await producer.StopAsync(); }

            var received = await Task.WhenAll(
                    primaryReceived.Task,
                    routedReceived.Task)
                .WaitAsync(TestTimeout);

            received.Should().ContainSingle(item => item.Destination == source);
            received.Should().ContainSingle(item =>
                item.Destination.ActorId == routedMailbox
                && item.Destination.Verb == source.Verb
                && item.Destination.EntityId == source.EntityId);
            received.Should().OnlyContain(item => item.Source == source
                && item.Dataset == "GLBX.MDP3" && item.Generation == generation);
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
        TaskCompletionSource<ReceivedRealtime> received)
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
                if (message is NatsOwnedEventMessage)
                    received.TrySetResult(new(subject, message.SourceSubject,
                        message.SourceDataset, message.SourceGenerationId));
                else
                    received.TrySetException(new InvalidOperationException(
                        $"Expected owned realtime payload, received {message.GetType().Name}."));
                message.Dispose();
                return ValueTask.FromResult(ActorAdmissionResult.AcceptedResult);
            });
        return queues;
    }

    [MessagePackObject]
    public sealed record TaggedRealtimeEvent : IEvent<ActorEntityId>, IRealtimeSourceGeneration
    {
        [Key(0)] public ActorSubject Subject { get; init; }
        [Key(1)] public Guid Id { get; init; }
        [Key(2)] public ActorEntityId EntityId { get; init; } = ActorEntityId.Default;
        [Key(3)] public long EventId { get; init; }
        [Key(4)] public Guid CommandId { get; init; }
        [Key(5)] public string AggregateId { get; init; } = string.Empty;
        [Key(6)] public string EventSource { get; init; } = string.Empty;
        [Key(7)] public DateTime ReceivedOn { get; init; }
        [IgnoreMember] public string SourceDataset { get; init; } = string.Empty;
        [IgnoreMember] public Guid SourceGenerationId { get; init; }
        [IgnoreMember] public string UserName => string.Empty;
        [IgnoreMember] public string EventName => nameof(TaggedRealtimeEvent);
        [IgnoreMember] public EventType EventType => EventType.DomainEvent;
    }

    private sealed record ReceivedRealtime(ActorSubject Destination, ActorSubject Source,
        string? Dataset, Guid Generation);

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
