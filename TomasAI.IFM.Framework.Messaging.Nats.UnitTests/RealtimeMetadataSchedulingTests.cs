using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class RealtimeMetadataSchedulingTests
{
    static string Root() => Path.Combine(Path.GetTempPath(), "ifm-metadata-regression", Guid.NewGuid().ToString("N"));
    static RealtimeMailboxOptions Options(string root, RealtimeMailboxStorage storage) => new() { Enabled = true, CapacityQualified = true,
        Directory = root, Epoch = "metadata_" + Guid.NewGuid().ToString("N"), Storage = storage, CaptureMaximumDelay = TimeSpan.Zero, MaximumPayloadBytes = 1024 };
    static IActorSupervisor Supervisor(ActorSubject subject, ConcurrentQueue<int> seen)
    {
        var supervisor = Substitute.For<IActorSupervisor>(); var actor = Substitute.For<IActor>();
        var mailbox = Substitute.For<IActorMailbox>(); var queues = Substitute.For<IActorThreadQueues>(); actor.Mailbox.Returns(mailbox); mailbox.ThreadQueues.Returns(queues);
        supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor> { [subject.ActorId] = actor }); supervisor.ActorExists(subject.ActorId).Returns(true);
        supervisor.GetRealtimeRoutes(Arg.Any<ActorTypeId>()).Returns(ImmutableArray<RealtimeActorRoute>.Empty);
        queues.TryAdmitAsync(Arg.Any<IActorMessage>(), Arg.Any<ActorSubject>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var message = call.Arg<IActorMessage>(); seen.Enqueue(int.Parse(message.SourceSubject.Verb, System.Globalization.CultureInfo.InvariantCulture));
            ((IActorDeliveryCompletion)message).CompleteDeliveryAsync(true).GetAwaiter().GetResult(); message.Dispose();
            return ValueTask.FromResult(ActorAdmissionResult.AcceptedResult);
        });
        return supervisor;
    }

    [RealtimeBrokerTheory]
    [InlineData(RealtimeMailboxStorage.Memory)]
    [InlineData(RealtimeMailboxStorage.File)]
    public async Task Same_thread_publication_after_zero_pending_metadata_is_scheduled_without_fallback_INFO(RealtimeMailboxStorage storage)
    {
        var root = Root(); var options = Options(root, storage); var subject = new ActorSubject(ActorType.Realtime, "MetadataArrival", "1", "ES");
        var seen = new ConcurrentQueue<int>(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var first = Guid.NewGuid();
        var connection = new NatsProducerOptions { Url = Environment.GetEnvironmentVariable("IFM_TEST_REALTIME_NATS")! };
        try
        {
            await using var manager = new NatsConnectionManager();
            await using var hub = new DurableRealtimeMailboxHub(options, connection, manager, NullLogger<DurableRealtimeMailboxHub>.Instance);
            hub.BeforePhysicalSettlementAsync = async (_, id, ack, _, token) =>
            { if (id == first && ack) { entered.TrySetResult(); await release.Task.WaitAsync(token); } };
            await hub.StartAsync(Supervisor(subject, seen)); await hub.CaptureAsync(new(subject, first, new byte[] { 1 }));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(8));
            var held = Assert.Single(hub.GetMailboxSnapshots(0, 10)); Assert.True(held.Scheduled); Assert.Equal(0UL, held.BrokerPending);
            await hub.CaptureAsync(new(subject with { Verb = "2" }, Guid.NewGuid(), new byte[] { 2 }));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (hub.Metrics.PublishedDestinations < 2) await Task.Delay(10, timeout.Token);
            Assert.Equal(new[] { 1 }, seen); release.TrySetResult();
            while (hub.Metrics.AcknowledgedDeliveries != 2 || hub.PendingSourceCount != 0) await Task.Delay(10, timeout.Token);
            Assert.Equal(new[] { 1, 2 }, seen); Assert.Equal(0, hub.ConsumerRefreshCount); Assert.Equal(0, hub.BrokerUnacknowledgedMessages);
        }
        finally { release.TrySetResult(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [RealtimeBrokerTheory]
    [InlineData(RealtimeMailboxStorage.Memory)]
    [InlineData(RealtimeMailboxStorage.File)]
    public async Task Legacy_two_unacknowledged_physical_messages_force_empty_pull_refresh_and_remain_owned(RealtimeMailboxStorage storage)
    {
        var root = Root(); var options = Options(root, storage); var subject = new ActorSubject(ActorType.Realtime, "MetadataLegacy", "1", "ES");
        var captures = Enumerable.Range(1, 2).Select(index => {
            var target = subject with { Verb = index.ToString() }; return RealtimeMailboxCodec.Capture(new(target, Guid.NewGuid(), new byte[] { (byte)index }), [target], options.MaximumPayloadBytes);
        }).ToArray();
        var key = RealtimeMailboxCodec.MailboxKey(subject.ThreadId); var actorKey = RealtimeMailboxCodec.ActorKey(subject.Name);
        var streamName = options.StreamPrefix + "_" + options.Epoch + "_" + actorKey;
        var wireSubject = options.StreamPrefix + "." + options.Epoch + "." + actorKey + "." + key;
        var connection = new NatsProducerOptions { Url = Environment.GetEnvironmentVariable("IFM_TEST_REALTIME_NATS")! }; var seen = new ConcurrentQueue<int>();
        try
        {
            await using (var sourceOwner = new RealtimeSourceOutbox(options)) await sourceOwner.CaptureBatchAsync(captures, default);
            await using var manager = new NatsConnectionManager(); var js = await manager.GetJetStreamContextAsync(connection.Url);
            var ownerHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.MachineName + "|" + Path.GetFullPath(root).ToUpperInvariant())));
            await js.CreateStreamAsync(new StreamConfig(streamName, [options.StreamPrefix + "." + options.Epoch + "." + actorKey + ".>"]) {
                Storage = storage == RealtimeMailboxStorage.Memory ? StreamConfigStorage.Memory : StreamConfigStorage.File,
                Retention = StreamConfigRetention.Workqueue, Discard = StreamConfigDiscard.New, DiscardNewPerSubject = true,
                MaxMsgsPerSubject = options.MaximumMessagesPerMailbox, MaxBytes = options.MaximumActorBytes, MaxMsgSize = options.MaximumPayloadBytes + 65536,
                NumReplicas = 1, Description = "IFM realtime source owner " + ownerHash });
            foreach (var capture in captures) (await js.PublishAsync(wireSubject, RealtimeMailboxCodec.Delivery(capture, capture.Destinations[0]),
                serializer: new NatsByteArrayMessageSerializer(), opts: new NatsJSPubOpts { MsgId = capture.Key + "." + key })).EnsureSuccess();
            var legacy = await js.CreateOrUpdateConsumerAsync(streamName, new ConsumerConfig("m_" + key) { FilterSubject = wireSubject,
                AckPolicy = ConsumerConfigAckPolicy.Explicit, DeliverPolicy = ConsumerConfigDeliverPolicy.All, MaxAckPending = 64, MaxDeliver = -1, AckWait = TimeSpan.FromSeconds(30) });
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12)); var fetched = new List<byte[]>();
            await foreach (var item in legacy.FetchNoWaitAsync<byte[]>(new NatsJSFetchOpts { MaxMsgs = 2, Expires = TimeSpan.FromSeconds(2) }, new NatsByteArrayMessageSerializer(), timeout.Token))
                fetched.Add(item.Data!); // Deliberately no ACK: these are genuine abandoned legacy deliveries.
            Assert.Equal(2, fetched.Count); await legacy.RefreshAsync(timeout.Token); Assert.Equal(2, legacy.Info.NumAckPending); Assert.Equal(0UL, legacy.Info.NumPending);
            await using (var hub = new DurableRealtimeMailboxHub(options, connection, manager, NullLogger<DurableRealtimeMailboxHub>.Instance))
            {
                await hub.StartAsync(Supervisor(subject, seen));
                while (hub.ConsumerRefreshCount == 0) await Task.Delay(10, timeout.Token);
                Assert.Equal(2, hub.BrokerUnacknowledgedMessages); Assert.Equal(2, hub.PendingSourceCount);
                Assert.Equal(0, hub.Metrics.AcknowledgedDeliveries); Assert.Empty(seen);
                var current = await js.GetConsumerAsync(streamName, "m_" + key, cancellationToken: timeout.Token);
                Assert.Equal(1, current.Info.Config.MaxAckPending); Assert.Equal(2, current.Info.NumAckPending);
            }
            await using var retained = new RealtimeSourceOutbox(options);
            Assert.Equal(captures.Select(value => value.SourceHash), retained.Pending().Select(value => value.SourceHash));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
