using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NSubstitute;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class RealtimeActorStreamPreparationTests
{
    [RealtimeBrokerTheory]
    [InlineData(RealtimeMailboxStorage.Memory)]
    [InlineData(RealtimeMailboxStorage.File)]
    public async Task Startup_prepares_only_registered_realtime_actor_streams_and_unknown_entity_remains_a_real_first_consumer(RealtimeMailboxStorage storage)
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm-actor-stream-preparation", Guid.NewGuid().ToString("N"));
        var options = Options(root, storage, enabled: true); var first = new ActorSubject(ActorType.Realtime, "PreparedPrimary", "Updated", "unknown-" + Guid.NewGuid().ToString("N"));
        var second = first with { Name = "PreparedSecondary" }; var command = first with { ActorType = ActorType.Command, Name = "NotARealtimeStream" };
        var seen = new ConcurrentQueue<ActorSubject>(); var supervisor = Registry(first, second, command, seen);
        var connection = new NatsProducerOptions { Url = Environment.GetEnvironmentVariable("IFM_TEST_REALTIME_NATS")! };
        try
        {
            await using var manager = new NatsConnectionManager();
            await using var hub = new DurableRealtimeMailboxHub(options, connection, manager, NullLogger<DurableRealtimeMailboxHub>.Instance);
            await hub.StartAsync(supervisor);
            Assert.True(hub.IsRunning); Assert.Equal(0, hub.MailboxCount); Assert.Equal(0, hub.PendingSourceCount);
            Assert.Equal(0, hub.Metrics.CapturedSources); Assert.Equal(0, hub.Metrics.PublishedDestinations); Assert.Empty(seen);
            var js = await manager.GetJetStreamContextAsync(connection.Url);
            var owner = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.MachineName + "|" + Path.GetFullPath(root).ToUpperInvariant())));
            foreach (var subject in new[] { first, second })
            {
                var stream = await js.GetStreamAsync(Stream(options, subject)); var config = stream.Info.Config;
                Assert.Equal("IFM realtime source owner " + owner, config.Description);
                Assert.Equal(storage == RealtimeMailboxStorage.Memory ? StreamConfigStorage.Memory : StreamConfigStorage.File, config.Storage);
                Assert.Equal(StreamConfigRetention.Workqueue, config.Retention); Assert.Equal(StreamConfigDiscard.New, config.Discard);
                Assert.True(config.DiscardNewPerSubject); Assert.Equal(options.MaximumMessagesPerMailbox, config.MaxMsgsPerSubject);
                Assert.Equal(options.MaximumActorBytes, config.MaxBytes); Assert.Equal(options.MaximumPayloadBytes + 65536, config.MaxMsgSize);
                Assert.True(stream.Info.State.ConsumerCount == 0); Assert.True(stream.Info.State.Messages == 0);
            }
            var missing = await Assert.ThrowsAsync<NatsJSApiException>(async () => await js.GetStreamAsync(Stream(options, command)));
            Assert.Equal(404, missing.Error.Code);
            await hub.CaptureAsync(new(first, Guid.NewGuid(), new byte[] { 1 }));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (hub.PendingSourceCount != 0 || hub.Metrics.AcknowledgedDeliveries != 1) await Task.Delay(5, timeout.Token);
            Assert.Equal(first, Assert.Single(seen)); Assert.Equal(1, hub.MailboxCount);
            var active = await js.GetStreamAsync(Stream(options, first), cancellationToken: timeout.Token);
            var untouched = await js.GetStreamAsync(Stream(options, second), cancellationToken: timeout.Token);
            Assert.True(active.Info.State.ConsumerCount == 1); Assert.True(active.Info.State.Messages == 0);
            Assert.True(untouched.Info.State.ConsumerCount == 0); Assert.True(untouched.Info.State.Messages == 0);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [RealtimeBrokerTheory]
    [InlineData(RealtimeMailboxStorage.Memory)]
    [InlineData(RealtimeMailboxStorage.File)]
    public async Task Disabled_hub_does_not_prepare_runtime_streams_or_consult_actor_registry(RealtimeMailboxStorage storage)
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm-disabled-stream-preparation", Guid.NewGuid().ToString("N"));
        var options = Options(root, storage, enabled: false); var subject = new ActorSubject(ActorType.Realtime, "DisabledPreparedActor", "Updated", "ES");
        var supervisor = Registry(subject, subject with { Name = "Other" }, subject with { ActorType = ActorType.Command }, new()); supervisor.ClearReceivedCalls();
        var connection = new NatsProducerOptions { Url = Environment.GetEnvironmentVariable("IFM_TEST_REALTIME_NATS")! };
        await using var manager = new NatsConnectionManager();
        await using var hub = new DurableRealtimeMailboxHub(options, connection, manager, NullLogger<DurableRealtimeMailboxHub>.Instance);
        await hub.StartAsync(supervisor);
        Assert.False(hub.IsRunning); Assert.Equal(0, hub.MailboxCount); Assert.False(Directory.Exists(root)); Assert.Empty(supervisor.ReceivedCalls());
        var js = await manager.GetJetStreamContextAsync(connection.Url);
        var missing = await Assert.ThrowsAsync<NatsJSApiException>(async () => await js.GetStreamAsync(Stream(options, subject)));
        Assert.Equal(404, missing.Error.Code);
    }

    static RealtimeMailboxOptions Options(string root, RealtimeMailboxStorage storage, bool enabled) => new() { Enabled = enabled, CapacityQualified = enabled,
        Storage = storage, Directory = root, Epoch = "prepare_" + Guid.NewGuid().ToString("N"), MaximumMailboxes = 2 };
    static string Stream(RealtimeMailboxOptions options, ActorSubject subject) => options.StreamPrefix + "_" + options.Epoch + "_" + RealtimeMailboxCodec.ActorKey(subject.Name);
    static IActorSupervisor Registry(ActorSubject first, ActorSubject second, ActorSubject command, ConcurrentQueue<ActorSubject> seen)
    {
        var supervisor = Substitute.For<IActorSupervisor>(); var actors = new Dictionary<ActorMailboxId, IActor>();
        foreach (var subject in new[] { first, second, command })
        {
            var actor = Substitute.For<IActor>(); var mailbox = Substitute.For<IActorMailbox>(); var queues = Substitute.For<IActorThreadQueues>();
            actor.Mailbox.Returns(mailbox); mailbox.ThreadQueues.Returns(queues); actors.Add(subject.ActorId, actor);
            queues.TryAdmitAsync(Arg.Any<IActorMessage>(), Arg.Any<ActorSubject>(), Arg.Any<CancellationToken>()).Returns(call => {
                var message = call.Arg<IActorMessage>(); seen.Enqueue(message.Subject);
                ((IActorDeliveryCompletion)message).CompleteDeliveryAsync(true).GetAwaiter().GetResult(); message.Dispose();
                return ValueTask.FromResult(ActorAdmissionResult.AcceptedResult);
            });
        }
        supervisor.Children.Returns(actors); supervisor.ActorExists(Arg.Any<ActorMailboxId>()).Returns(true);
        supervisor.GetRealtimeRoutes(Arg.Any<ActorTypeId>()).Returns(ImmutableArray<RealtimeActorRoute>.Empty); return supervisor;
    }
}
