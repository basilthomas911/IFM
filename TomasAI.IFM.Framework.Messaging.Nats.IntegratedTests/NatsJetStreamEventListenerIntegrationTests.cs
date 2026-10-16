using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;
using NSubstitute;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Framework.Messaging.Nats.IntegratedTests;

[Trait("Category", "Integration")]
public sealed class NatsJetStreamEventListenerIntegrationTests
{
    static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    readonly string _url = Environment.GetEnvironmentVariable("IFM_NATS_URL") ?? "nats://localhost:4222";

    [Fact]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
    public async Task Success_acknowledges_once_and_filters_unconfigured_verbs()
    {
        var resources = CreateResources();
var admin = await (CreateAdminAsync()).ConfigureAwait(true);
await using var adminDisposal = admin.ConfigureAwait(true);
        var jetStream = admin.CreateJetStreamContext();
        var calls = new ConcurrentQueue<string>();
        var handled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = CreateListener(resources);

        try
        {
            await listener.StartAsync(resources.ListenerId, CreateEventMap(resources.Mailbox, "Started"), (verb, _) =>
            {
                calls.Enqueue(verb);
                handled.TrySetResult();
                return ValueTask.CompletedTask;
            });

            await PublishAsync(jetStream, $"{resources.Mailbox}.Ignored.operation-1");
            await PublishAsync(jetStream, $"{resources.Mailbox}.Started.operation-1");
            await handled.Task.WaitAsync(TestTimeout);
            await WaitUntilAsync(async () =>
            {
                var consumer = await (GetConsumerAsync(jetStream, listener, resources)).ConfigureAwait(true);
                return consumer.Info.NumAckPending == 0 && consumer.Info.AckFloor.ConsumerSeq >= 2;
            });
            var stream = await jetStream.GetStreamAsync(listener.StreamName);
            stream.Info.Config.Retention.Should().Be(StreamConfigRetention.Interest);

            calls.Should().Equal("Started");
            listener.MessageCount.Should().Be(2);
        }
        finally
        {
            await listener.StopAsync();
            await CleanupAsync(jetStream, listener, resources);
        }
    }

    [Fact]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
    public async Task Handler_failure_is_negatively_acknowledged_and_redelivered()
    {
        var resources = CreateResources();
var admin = await (CreateAdminAsync()).ConfigureAwait(true);
await using var adminDisposal = admin.ConfigureAwait(true);
        var jetStream = admin.CreateJetStreamContext();
        var redelivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        var listener = CreateListener(resources, negativeAcknowledgeDelay: TimeSpan.FromMilliseconds(100));

        try
        {
            await listener.StartAsync(resources.ListenerId, CreateEventMap(resources.Mailbox, "Execute"), (_, _) =>
            {
                if (Interlocked.Increment(ref attempts) == 1)
                    throw new InvalidOperationException("journal admission failed");
                redelivered.TrySetResult();
                return ValueTask.CompletedTask;
            });

            await PublishAsync(jetStream, $"{resources.Mailbox}.Execute.operation-1");
            await redelivered.Task.WaitAsync(TestTimeout);

            attempts.Should().Be(2);
            await WaitUntilAsync(async () =>
            {
                var acknowledged = await (GetConsumerAsync(jetStream, listener, resources)).ConfigureAwait(true);
                return acknowledged.Info.AckFloor.ConsumerSeq >= 2;
            });
            var consumer = await GetConsumerAsync(jetStream, listener, resources);
            consumer.Info.Delivered.ConsumerSeq.Should().BeGreaterThanOrEqualTo(2);
            consumer.Info.AckFloor.ConsumerSeq.Should().BeGreaterThanOrEqualTo(2);
            listener.MessageCount.Should().Be(2);
        }
        finally
        {
            await listener.StopAsync();
            await CleanupAsync(jetStream, listener, resources);
        }
    }

    [Fact]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
    public async Task Durable_listener_resumes_unacknowledged_delivery_after_restart()
    {
        var resources = CreateResources();
var admin = await (CreateAdminAsync()).ConfigureAwait(true);
await using var adminDisposal = admin.ConfigureAwait(true);
        var jetStream = admin.CreateJetStreamContext();
        var firstAttempt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = CreateListener(resources, negativeAcknowledgeDelay: TimeSpan.FromSeconds(2));

        try
        {
            await first.StartAsync(resources.ListenerId, CreateEventMap(resources.Mailbox, "Execute"), (_, _) =>
            {
                firstAttempt.TrySetResult();
                throw new InvalidOperationException("simulate process failure");
            });
            await PublishAsync(jetStream, $"{resources.Mailbox}.Execute.operation-1");
            await firstAttempt.Task.WaitAsync(TestTimeout);
            await first.StopAsync();

            var resumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var second = CreateListener(resources, negativeAcknowledgeDelay: TimeSpan.FromMilliseconds(50));
            try
            {
                await second.StartAsync(resources.ListenerId, CreateEventMap(resources.Mailbox, "Execute"), (_, _) =>
                {
                    resumed.TrySetResult();
                    return ValueTask.CompletedTask;
                });

                await resumed.Task.WaitAsync(TestTimeout);
                var consumer = await GetConsumerAsync(jetStream, second, resources);
                consumer.Info.Delivered.ConsumerSeq.Should().BeGreaterThanOrEqualTo(2);
            }
            finally
            {
                await second.StopAsync();
            }
        }
        finally
        {
            await first.StopAsync();
            await CleanupAsync(jetStream, first, resources);
        }
    }

    [Fact]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
    public async Task Stop_drains_admitted_handler_before_returning()
    {
        var resources = CreateResources();
var admin = await (CreateAdminAsync()).ConfigureAwait(true);
await using var adminDisposal = admin.ConfigureAwait(true);
        var jetStream = admin.CreateJetStreamContext();
        var admitted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = CreateListener(resources);

        try
        {
            await listener.StartAsync(resources.ListenerId, CreateEventMap(resources.Mailbox, "Execute"), async (_, _) =>
            {
                admitted.TrySetResult();
                await (release.Task).ConfigureAwait(true);
            });
            await PublishAsync(jetStream, $"{resources.Mailbox}.Execute.operation-1");
            await admitted.Task.WaitAsync(TestTimeout);

            var stop = listener.StopAsync().AsTask();
            await Task.Delay(100);
            stop.IsCompleted.Should().BeFalse();
            release.TrySetResult();
            await stop.WaitAsync(TestTimeout);

            var consumer = await GetConsumerAsync(jetStream, listener, resources);
            consumer.Info.NumAckPending.Should().Be(0);
        }
        finally
        {
            release.TrySetResult();
            await listener.StopAsync();
            await CleanupAsync(jetStream, listener, resources);
        }
    }

    [Fact]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
    public async Task Bounded_ack_window_leaves_unadmitted_message_for_durable_restart()
    {
        var resources = CreateResources();
var admin = await (CreateAdminAsync()).ConfigureAwait(true);
await using var adminDisposal = admin.ConfigureAwait(true);
        var jetStream = admin.CreateJetStreamContext();
        var firstAdmitted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = CreateListener(resources, dispatcherCapacity: 1, maxAckPending: 1);

        try
        {
            await first.StartAsync(resources.ListenerId, CreateEventMap(resources.Mailbox, "Execute"), async (_, _) =>
            {
                firstAdmitted.TrySetResult();
                await (release.Task).ConfigureAwait(true);
            });
            await PublishAsync(jetStream, $"{resources.Mailbox}.Execute.operation-1");
            await PublishAsync(jetStream, $"{resources.Mailbox}.Execute.operation-2");
            await firstAdmitted.Task.WaitAsync(TestTimeout);

            var consumer = await GetConsumerAsync(jetStream, first, resources);
            consumer.Info.NumAckPending.Should().Be(1);
            consumer.Info.NumPending.Should().BeGreaterThanOrEqualTo(1);

            var stop = first.StopAsync().AsTask();
            release.TrySetResult();
            await stop.WaitAsync(TestTimeout);

            var secondHandled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var second = CreateListener(resources, dispatcherCapacity: 1, maxAckPending: 1);
            try
            {
                await second.StartAsync(resources.ListenerId, CreateEventMap(resources.Mailbox, "Execute"), (_, message) =>
                {
                    if (message.Subject.EndsWith("operation-2", StringComparison.Ordinal))
                        secondHandled.TrySetResult();
                    return ValueTask.CompletedTask;
                });
                await secondHandled.Task.WaitAsync(TestTimeout);
            }
            finally
            {
                await second.StopAsync();
            }
        }
        finally
        {
            release.TrySetResult();
            await first.StopAsync();
            await CleanupAsync(jetStream, first, resources);
        }
    }

    NatsJetStreamEventListener CreateListener(
        TestResources resources,
        TimeSpan? negativeAcknowledgeDelay = null,
        int dispatcherCapacity = 4,
        int maxAckPending = 4) => new(
        new NatsJetStreamEventListenerOptions
        {
            Url = _url,
            StreamName = resources.StreamName,
            DurableConsumerNamePrefix = resources.DurablePrefix,
            AckWait = TimeSpan.FromSeconds(3),
            NegativeAcknowledgeDelay = negativeAcknowledgeDelay ?? TimeSpan.FromMilliseconds(100),
            DispatcherCount = 1,
            DispatcherCapacity = dispatcherCapacity,
            MaxAckPending = maxAckPending,
            MaxMessages = maxAckPending,
            ThresholdMessages = 1
        },
        Substitute.For<ILogger>());

    async ValueTask<NatsClient> CreateAdminAsync()
    {
        var client = new NatsClient(_url);
        await (client.ConnectAsync()).ConfigureAwait(true);
        return client;
    }

    static Dictionary<ActorMailboxId, List<string>> CreateEventMap(
        ActorMailboxId mailbox,
        params string[] verbs) => new() { [mailbox] = [.. verbs] };

    static async ValueTask PublishAsync(INatsJSContext jetStream, string subject)
    {
        var acknowledgement = await (jetStream.PublishAsync(
            subject,
            new byte[] { 1, 2, 3 },
            serializer: new NatsByteArrayMessageSerializer())).ConfigureAwait(true);
        acknowledgement.EnsureSuccess();
    }

    static async ValueTask<INatsJSConsumer> GetConsumerAsync(
        INatsJSContext jetStream,
        NatsJetStreamEventListener listener,
        TestResources resources)
    {
        var name = NatsJetStreamEventListener.CreateDurableConsumerName(
            resources.DurablePrefix,
            resources.ListenerId,
            resources.Mailbox);
        return await (jetStream.GetConsumerAsync(listener.StreamName, name)).ConfigureAwait(true);
    }

    static async Task WaitUntilAsync(Func<Task<bool>> predicate)
    {
        using var timeout = new CancellationTokenSource(TestTimeout);
        while (!await (predicate()).ConfigureAwait(true))
            await (Task.Delay(25, timeout.Token)).ConfigureAwait(true);
    }

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

    static async ValueTask CleanupAsync(
        INatsJSContext jetStream,
        NatsJetStreamEventListener listener,
        TestResources resources)
    {
        var consumerName = NatsJetStreamEventListener.CreateDurableConsumerName(
            resources.DurablePrefix,
            resources.ListenerId,
            resources.Mailbox);
        if (!string.IsNullOrEmpty(listener.StreamName))
        {
            try
            {
                await (jetStream.DeleteConsumerAsync(listener.StreamName, consumerName)).ConfigureAwait(true);
            }
            catch (NatsJSApiException)
            {
            }
        }

        if (string.Equals(listener.StreamName, resources.StreamName, StringComparison.Ordinal))
            await (TryDeleteStreamAsync(jetStream, resources.StreamName)).ConfigureAwait(true);
    }

    static TestResources CreateResources()
    {
        var id = Guid.NewGuid().ToString("N");
        return new TestResources(
            $"IFM_JS_LISTENER_{id}",
            $"js-listener-{id}",
            $"listener-{id}",
            new ActorMailboxId(ActorType.Event, $"Backup{id}"));
    }

    sealed record TestResources(
        string StreamName,
        string DurablePrefix,
        string ListenerId,
        ActorMailboxId Mailbox);
}
