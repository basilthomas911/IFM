using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using NSubstitute;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.IntegratedTests;

[Trait("Category", "Integration")]
public sealed class NatsTransportOverloadIntegrationTests
{
    static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    readonly string _url = Environment.GetEnvironmentVariable("IFM_NATS_URL") ?? "nats://localhost:4222";

    [Theory]
    [InlineData(ActorType.Command, true)]
    [InlineData(ActorType.Command, false)]
    [InlineData(ActorType.Query, true)]
    [InlineData(ActorType.Query, false)]
    public async Task CoreRequest_OverloadReturnsTypedRetryableFailure(
        ActorType actorType,
        bool useOwnedPayload)
    {
        var actorName = $"Overload{Guid.NewGuid():N}";
        var subject = new ActorSubject(actorType, actorName, "Test", "42");
        var admission = CreateEnforcedAdmissionOptions();
        var controller = new ActorAdmissionController(admission);
        var supervisor = Substitute.For<IActorSupervisor>();
        var mailbox = new ActorMailbox(supervisor, subject.ActorId, 4, controller);
        var actor = Substitute.For<IActor>();
        actor.Id.Returns(subject.ActorId);
        actor.Mailbox.Returns(mailbox);
        supervisor.Children.Returns(
            new Dictionary<ActorMailboxId, IActor> { [subject.ActorId] = actor });
        var options = new NatsConsumerOptions
        {
            Url = _url,
            DispatcherCount = 1,
            DispatcherCapacity = 8,
            SubscriptionCapacity = 8,
            UseOwnedCommandPayloads = useOwnedPayload,
            UseOwnedQueryPayloads = useOwnedPayload,
            FireAndForgetTraffic = new Dictionary<ActorType, CoreNatsTrafficClass>
            {
                [actorType] = CoreNatsTrafficClass.RequestReplyOnly
            }
        };
        var consumer = new NatsActorConsumer(
            options,
            Substitute.For<ILogger>(),
            admissionOptions: admission);

        try
        {
            await consumer.StartAsync(supervisor, actorType, $"overload-{Guid.NewGuid():N}");
            await Task.Delay(250);
            await using var client = new NatsClient(_url);
            await client.ConnectAsync();
            using var timeout = new CancellationTokenSource(TestTimeout);

            var request = new byte[512];
            RandomNumberGenerator.Fill(request);
            var response = await client.RequestAsync<byte[], ServiceResult<GuidResult>>(
                subject.ToString(),
                request,
                requestSerializer: NatsMessagePackSerializer<byte[]>.Default,
                replySerializer: NatsMessagePackSerializer<ServiceResult<GuidResult>>.Default,
                cancellationToken: timeout.Token);

            response.Data.Should().NotBeNull();
            response.Data!.Success.Should().BeFalse();
            response.Data.ErrorCode.Should().Be(-429);
            response.Data.ErrorMessage.Should().Be(NatsTransportOverload.RetryableMessage);
            response.Data.Value.Should().BeNull();
        }
        finally
        {
            await consumer.StopAsync();
        }
    }

    [Fact]
    public async Task JetStreamOverload_DelaysNakAndRedeliversAfterAdmissionRecovers()
    {
        var resourceId = Guid.NewGuid().ToString("N");
        var streamName = $"IFM_OVERLOAD_{resourceId}";
        var durableName = $"overload-{resourceId}";
        var actorName = $"OverloadEvent{resourceId}";
        var subject = new ActorSubject(ActorType.Event, actorName, "Recorded", "42");
        var queues = Substitute.For<IActorThreadQueues>();
        var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstAttemptTimestamp = 0L;
        var secondAttemptTimestamp = 0L;
        var attempts = 0;
        queues.TryAdmitAsync(
                Arg.Any<IActorMessage>(),
                Arg.Any<ActorSubject>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var attempt = Interlocked.Increment(ref attempts);
                if (attempt == 1)
                {
                    firstAttemptTimestamp = Stopwatch.GetTimestamp();
                    return ValueTask.FromResult(
                        ActorAdmissionResult.Rejected(ActorAdmissionReason.MailboxLimit));
                }

                secondAttemptTimestamp = Stopwatch.GetTimestamp();
                callInfo.Arg<IActorMessage>().Dispose();
                recovered.TrySetResult();
                return ValueTask.FromResult(ActorAdmissionResult.AcceptedResult);
            });
        var supervisor = CreateSupervisor(subject.ActorId, queues);
        supervisor.ActorExists(subject.ActorId).Returns(true);
        supervisor.GetEventRoutes(Arg.Any<ActorTypeId>())
            .Returns(ImmutableHashSet<ActorMailboxId>.Empty);
        var delay = TimeSpan.FromMilliseconds(150);
        var options = new NatsJetStreamConsumerOptions
        {
            Url = _url,
            StreamName = streamName,
            DurableConsumerName = durableName,
            FilterSubject = subject.ToString(),
            DispatcherCount = 1,
            DispatcherCapacity = 8,
            MaxAckPending = 8,
            MaxMessages = 8,
            ThresholdMessages = 1,
            UseOwnedEventPayloads = true
        };
        var admission = new ActorAdmissionOptions
        {
            JetStreamNakDelayMilliseconds = (int)delay.TotalMilliseconds
        };
        var consumer = new NatsJetStreamActorConsumer(
            options,
            Substitute.For<ILogger>(),
            admissionOptions: admission);
        await using var client = new NatsClient(_url);
        await client.ConnectAsync();
        var jetStream = client.CreateJetStreamContext();

        try
        {
            await consumer.StartAsync(supervisor, ActorType.Event, durableName);
            var publish = await jetStream.PublishAsync(
                subject.ToString(),
                new byte[128],
                serializer: NatsDefaultSerializer<byte[]>.Default);
            publish.EnsureSuccess();

            await recovered.Task.WaitAsync(TestTimeout);

            attempts.Should().Be(2);
            Stopwatch.GetElapsedTime(firstAttemptTimestamp, secondAttemptTimestamp)
                .Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(100));
            var serverConsumer = await jetStream.GetConsumerAsync(consumer.StreamName, consumer.ConsumerName);
            serverConsumer.Info.Delivered.ConsumerSeq.Should().BeGreaterThanOrEqualTo(2);
        }
        finally
        {
            await consumer.StopAsync();
            await TryDeleteConsumerAsync(jetStream, consumer.StreamName, consumer.ConsumerName);
            if (string.Equals(consumer.StreamName, streamName, StringComparison.Ordinal))
                await TryDeleteStreamAsync(jetStream, streamName);
        }
    }

    static IActorSupervisor CreateSupervisor(
        ActorMailboxId mailboxId,
        IActorThreadQueues queues)
    {
        var mailbox = Substitute.For<IActorMailbox>();
        mailbox.ThreadQueues.Returns(queues);
        var actor = Substitute.For<IActor>();
        actor.Id.Returns(mailboxId);
        actor.Mailbox.Returns(mailbox);
        var supervisor = Substitute.For<IActorSupervisor>();
        supervisor.Children.Returns(
            new Dictionary<ActorMailboxId, IActor> { [mailboxId] = actor });
        return supervisor;
    }

    static ActorAdmissionOptions CreateEnforcedAdmissionOptions()
        => new()
        {
            Mode = ActorAdmissionMode.Enforce,
            GlobalMessageLimit = 8,
            GlobalByteLimit = 1_024,
            MaximumPayloadBytes = 64,
            DefaultActorTypeMessageLimit = 8,
            DefaultActorTypeByteLimit = 1_024,
            DefaultMailboxMessageLimit = 4,
            JetStreamNakDelayMilliseconds = 150,
            OverloadErrorCode = -429
        };

    static async ValueTask TryDeleteStreamAsync(INatsJSContext jetStream, string streamName)
    {
        try
        {
            await jetStream.DeleteStreamAsync(streamName);
        }
        catch (NatsJSApiException)
        {
        }
    }

    static async ValueTask TryDeleteConsumerAsync(
        INatsJSContext jetStream,
        string streamName,
        string consumerName)
    {
        if (string.IsNullOrEmpty(streamName) || string.IsNullOrEmpty(consumerName))
            return;

        try
        {
            await jetStream.DeleteConsumerAsync(streamName, consumerName);
        }
        catch (NatsJSApiException)
        {
        }
    }
}
