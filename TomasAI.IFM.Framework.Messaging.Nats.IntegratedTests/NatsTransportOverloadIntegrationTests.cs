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

    /// <summary>Real Core NATS retains a burst while admission is blocked and keeps optional dispatch independent.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Protected_realtime_burst_waits_without_loss_and_optional_lane_keeps_running(bool useOwnedPayload)
    {
        var protectedSubject = new ActorSubject(ActorType.Realtime, "Protected" + Guid.NewGuid().ToString("N"), "Updated", "ES");
        var optionalSubject = new ActorSubject(ActorType.Realtime, "Optional" + Guid.NewGuid().ToString("N"), "Updated", "ES");
        var queues = Substitute.For<IActorThreadQueues>();
        var optionalQueues = Substitute.For<IActorThreadQueues>();
        var supervisor = CreateSupervisor(protectedSubject.ActorId, queues);
        var optionalActor = Substitute.For<IActor>();
        var optionalMailbox = Substitute.For<IActorMailbox>();
        optionalActor.Mailbox.Returns(optionalMailbox); optionalMailbox.ThreadQueues.Returns(optionalQueues);
        supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor>
        { [protectedSubject.ActorId] = supervisor.Children[protectedSubject.ActorId], [optionalSubject.ActorId] = optionalActor });
        supervisor.ActorExists(Arg.Any<ActorMailboxId>()).Returns(true);
        supervisor.GetRealtimeRoutes(Arg.Any<ActorTypeId>()).Returns(ImmutableArray<RealtimeActorRoute>.Empty);
        var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var optionalSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var complete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var open = false;
        const int count = 10000; // Exceeds the Development realtime actor-type budget.
        var received = new List<int>();
        queues.TryAdmitAsync(Arg.Any<IActorMessage>(), protectedSubject, Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (!Volatile.Read(ref open)) { blocked.TrySetResult(); return ValueTask.FromResult(ActorAdmissionResult.Rejected(ActorAdmissionReason.ActorTypeMessageLimit)); }
            var message = call.Arg<IActorMessage>();
            received.Add((int)message.AsEvent<NatsRealtimeRoutingIntegrationTests.TaggedRealtimeEvent>()!.EventId);
            message.Dispose();
            if (received.Count == count) complete.TrySetResult();
            return ValueTask.FromResult(ActorAdmissionResult.AcceptedResult);
        });
        optionalQueues.TryAdmitAsync(Arg.Any<IActorMessage>(), optionalSubject, Arg.Any<CancellationToken>()).Returns(_ =>
        { optionalSeen.TrySetResult(); return ValueTask.FromResult(ActorAdmissionResult.Rejected(ActorAdmissionReason.ActorTypeMessageLimit)); });
        var admission = CreateEnforcedAdmissionOptions();
        admission.ProtectedRealtimeActors = [protectedSubject.Name]; admission.ProtectedRealtimeMessageReserve = 1; admission.ProtectedRealtimeByteReserve = 64;
        var options = new NatsConsumerOptions { Url = _url, DispatcherCount = 1, DispatcherCapacity = 8, SubscriptionCapacity = 64,
            UseOwnedRealtimePayloads = useOwnedPayload,
            FireAndForgetTraffic = new Dictionary<ActorType, CoreNatsTrafficClass> { [ActorType.Realtime] = CoreNatsTrafficClass.Optional } };
        var logger = Substitute.For<ILogger>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(call => call.Arg<LogLevel>() >= LogLevel.Warning);
        await using var consumer = new NatsActorConsumer(options, logger, admissionOptions: admission);
        await consumer.StartAsync(supervisor, ActorType.Realtime, "protected-burst");
        await using var client = new NatsClient(_url);
        await client.ConnectAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var serializer = NatsMessagePackSerializer<NatsRealtimeRoutingIntegrationTests.TaggedRealtimeEvent>.Default;
        await client.PublishAsync(protectedSubject.ToString(), new NatsRealtimeRoutingIntegrationTests.TaggedRealtimeEvent { Subject = protectedSubject, EventId = 0 }, serializer: serializer, cancellationToken: timeout.Token);
        await blocked.Task.WaitAsync(timeout.Token);
        await client.PublishAsync(optionalSubject.ToString(), new NatsRealtimeRoutingIntegrationTests.TaggedRealtimeEvent { Subject = optionalSubject, EventId = 0 }, serializer: serializer, cancellationToken: timeout.Token);
        Assert.True(await Task.WhenAny(optionalSeen.Task, Task.Delay(TimeSpan.FromSeconds(3), timeout.Token)) == optionalSeen.Task,
            $"Optional calls={optionalQueues.ReceivedCalls().Count()}, protected calls={queues.ReceivedCalls().Count()}, children={string.Join(',', supervisor.Children.Keys)}\n" +
            string.Join(Environment.NewLine, logger.ReceivedCalls().Where(call => call.GetMethodInfo().Name == "Log")
                .Select(call => string.Join(" ", call.GetArguments().Select(arg => arg?.ToString())))));
        Assert.Empty(received);
        Volatile.Write(ref open, true);
        for (var index = 1; index < count; index++)
            await client.PublishAsync(protectedSubject.ToString(), new NatsRealtimeRoutingIntegrationTests.TaggedRealtimeEvent { Subject = protectedSubject, EventId = index }, serializer: serializer, cancellationToken: timeout.Token);
        await complete.Task.WaitAsync(timeout.Token);
        Assert.Equal(Enumerable.Range(0, count), received);
    }

    [Theory]
    [InlineData(ActorType.Command, true)]
    [InlineData(ActorType.Command, false)]
    [InlineData(ActorType.Query, true)]
    [InlineData(ActorType.Query, false)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA2000",Justification="The test deliberately exercises transferred resource ownership and explicit disposal assertions.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
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
var client = new NatsClient(_url);
await using var clientDisposal = client.ConfigureAwait(true);
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
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA2000",Justification="The test deliberately exercises transferred resource ownership and explicit disposal assertions.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA2012",Justification="This ValueTask invocation configures or verifies an NSubstitute call, rather than executing asynchronous work.")]
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
var client = new NatsClient(_url);
await using var clientDisposal = client.ConfigureAwait(true);
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
            await (jetStream.DeleteStreamAsync(streamName)).ConfigureAwait(true);
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
            await (jetStream.DeleteConsumerAsync(streamName, consumerName)).ConfigureAwait(true);
        }
        catch (NatsJSApiException)
        {
        }
    }
}
