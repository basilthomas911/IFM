using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class RealtimeBrokerTheoryAttribute : TheoryAttribute
{
    public RealtimeBrokerTheoryAttribute()
    { if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IFM_TEST_REALTIME_NATS"))) Skip = "Requires disposable JetStream endpoint IFM_TEST_REALTIME_NATS."; }
}

public sealed class RealtimeDurableBrokerTests
{
    [RealtimeBrokerTheory]
    [InlineData(RealtimeMailboxStorage.Memory)]
    [InlineData(RealtimeMailboxStorage.File)]
    public async Task FailedHeadRedeliversBeforeSuccessorAndUnrelatedMailboxContinues(RealtimeMailboxStorage storage)
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm-broker-test-" + Guid.NewGuid().ToString("N"));
        var options = new RealtimeMailboxOptions { Enabled = true, CapacityQualified = true, Directory = root,
            Epoch = "test_" + Guid.NewGuid().ToString("N"), Storage = storage, DeliveryConcurrency = 2, FailureRedeliveryDelay = TimeSpan.FromMilliseconds(50) };
        var subject = new ActorSubject(ActorType.Realtime, "TestPrice", "Updated", "A");
        var supervisor = Substitute.For<IActorSupervisor>();
        var actor = Substitute.For<IActor>(); var mailbox = Substitute.For<IActorMailbox>(); var queues = Substitute.For<IActorThreadQueues>();
        actor.Mailbox.Returns(mailbox); mailbox.ThreadQueues.Returns(queues);
        supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor> { [subject.ActorId] = actor });
        supervisor.ActorExists(subject.ActorId).Returns(true);
        supervisor.GetRealtimeRoutes(Arg.Any<ActorTypeId>()).Returns(ImmutableArray<RealtimeActorRoute>.Empty);
        var deliveries = new ConcurrentQueue<string>(); var headTries = 0;
        var held = new TaskCompletionSource<RealtimeBrokerActorMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        queues.TryAdmitAsync(Arg.Any<IActorMessage>(), Arg.Any<ActorSubject>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var message = (RealtimeBrokerActorMessage)call.Arg<IActorMessage>();
            var target = call.Arg<ActorSubject>(); deliveries.Enqueue(target.EntityId + ":" + target.Verb);
            if (target.Verb == "Hold") { held.TrySetResult(message); return ValueTask.FromResult(ActorAdmissionResult.AcceptedResult); }
            var success = target.EntityId != "A" || target.Verb != "First" || Interlocked.Increment(ref headTries) > 1;
            message.CompleteDeliveryAsync(success).GetAwaiter().GetResult();
            message.Dispose();
            return ValueTask.FromResult(ActorAdmissionResult.AcceptedResult);
        });
        var connection = new NatsProducerOptions { Url = Environment.GetEnvironmentVariable("IFM_TEST_REALTIME_NATS")! };
        try
        {
            await using var manager = new NatsConnectionManager();
            await using (var hub = new DurableRealtimeMailboxHub(options, connection, manager, NullLogger<DurableRealtimeMailboxHub>.Instance))
            {
                await hub.StartAsync(supervisor);
                await hub.CaptureAsync(new(subject with { Verb = "First" }, Guid.NewGuid(), new byte[] { 1 }));
                await hub.CaptureAsync(new(subject with { Verb = "Second" }, Guid.NewGuid(), new byte[] { 2 }));
                await hub.CaptureAsync(new(subject with { EntityId = "B", Verb = "Other" }, Guid.NewGuid(), new byte[] { 3 }));
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                while (hub.Metrics.AcknowledgedDeliveries < 3 && !timeout.IsCancellationRequested) await Task.Delay(20);
                Assert.True(hub.Metrics.AcknowledgedDeliveries >= 3, $"Transport: {hub.LastFailure}; delivered={string.Join(',', deliveries)}; published={hub.Metrics.PublishedDestinations}; pending={hub.PendingSourceCount}");
                Assert.Equal(new[] { "A:First", "A:First", "A:Second" }, deliveries.Where(v => v.StartsWith("A:", StringComparison.Ordinal)));
                Assert.Contains("B:Other", deliveries);
                Assert.Equal(1, hub.Metrics.FailedDeliveries);
                var generation = Guid.NewGuid();
                await hub.CaptureAsync(new(subject with { Verb = "Hold" }, Guid.NewGuid(), new byte[] { 4 }, Dataset: "GLBX.MDP3", SourceGenerationId: generation));
                var inFlight = await held.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var drained = hub.WaitForDrainAsync("GLBX.MDP3", generation).AsTask();
                Assert.False(drained.IsCompleted);
                inFlight.ReleasePayload();
                Assert.False(drained.IsCompleted);
                await inFlight.CompleteDeliveryAsync(true); inFlight.Dispose();
                await drained.WaitAsync(TimeSpan.FromSeconds(5));
                while (hub.PendingSourceCount > 0) await Task.Delay(20, timeout.Token);
            }
            await using (var restarted = new DurableRealtimeMailboxHub(options, connection, manager, NullLogger<DurableRealtimeMailboxHub>.Instance))
            {
                await restarted.StartAsync(supervisor);
                // Completed protocol2 sources need no new per-mailbox registry file.
                // Legacy registry files remain readable; no required recipient is lost.
                Assert.Equal(0, restarted.MailboxCount);
                Assert.Equal(0, restarted.PendingSourceCount);
            }
            var js = await manager.GetJetStreamContextAsync(connection.Url);
            var streamName = options.StreamPrefix + "_" + options.Epoch + "_" + RealtimeMailboxCodec.ActorKey(subject.Name);
            await js.DeleteStreamAsync(streamName);
            await using (var missingBrokerState = new DurableRealtimeMailboxHub(options, connection, manager, NullLogger<DurableRealtimeMailboxHub>.Instance))
            {
                await missingBrokerState.StartAsync(supervisor);
                Assert.True(missingBrokerState.IsRunning);
                await missingBrokerState.CaptureAsync(new(subject with { Verb = "AfterBrokerRebuild" }, Guid.NewGuid(), new byte[] { 5 }));
                using var recovered = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                while (missingBrokerState.Metrics.AcknowledgedDeliveries < 1) await Task.Delay(20, recovered.Token);
            }
        }
        finally { Directory.Delete(root, true); }
    }
    [RealtimeBrokerTheory]
    [InlineData(RealtimeMailboxStorage.Memory)]
    [InlineData(RealtimeMailboxStorage.File)]
    public async Task BoundedBatchRetriesHeadAndRestoresUnfinishedSourceAfterBrokerLoss(RealtimeMailboxStorage storage)
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm-broker-batch-" + Guid.NewGuid().ToString("N"));
        var options = new RealtimeMailboxOptions { Enabled = true, CapacityQualified = true, Directory = root,
            Epoch = "batch_" + Guid.NewGuid().ToString("N"), Storage = storage, FailureRedeliveryDelay = TimeSpan.FromMilliseconds(50) };
        var subject = new ActorSubject(ActorType.Realtime, "BatchPrice", "1", "ES");
        var supervisor = Substitute.For<IActorSupervisor>();
        var actor = Substitute.For<IActor, IBatchedRealtimeActor>(); var mailbox = Substitute.For<IActorMailbox>(); var queues = Substitute.For<IActorThreadQueues>();
        actor.Mailbox.Returns(mailbox); mailbox.ThreadQueues.Returns(queues);
        supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor> { [subject.ActorId] = actor });
        supervisor.ActorExists(subject.ActorId).Returns(true);
        supervisor.GetRealtimeRoutes(Arg.Any<ActorTypeId>()).Returns(ImmutableArray<RealtimeActorRoute>.Empty);
        var succeeded = new ConcurrentQueue<int>(); var firstFailure = 0; var holding = false;
        var held = new TaskCompletionSource<IRealtimeBrokerBatch>(TaskCreationOptions.RunContinuationsAsynchronously);
        queues.TryAdmitAsync(Arg.Any<IActorMessage>(), Arg.Any<ActorSubject>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var group = Assert.IsAssignableFrom<IRealtimeBrokerBatch>(call.Arg<IActorMessage>());
            Assert.InRange(group.Messages.Count, 1, 64);
            if (Volatile.Read(ref holding)) { held.TrySetResult(group); return ValueTask.FromResult(ActorAdmissionResult.AcceptedResult); }
            var success = Interlocked.Increment(ref firstFailure) != 1;
            foreach (var item in group.Messages)
            {
                if (success) succeeded.Enqueue(int.Parse(item.Subject.Verb, System.Globalization.CultureInfo.InvariantCulture));
                ((IActorDeliveryCompletion)item).CompleteDeliveryAsync(success).GetAwaiter().GetResult();
            }
            group.Dispose(); return ValueTask.FromResult(ActorAdmissionResult.AcceptedResult);
        });
        var connection = new NatsProducerOptions { Url = Environment.GetEnvironmentVariable("IFM_TEST_REALTIME_NATS")! };
        try
        {
            await using var manager = new NatsConnectionManager();
            await using (var hub = new DurableRealtimeMailboxHub(options, connection, manager, NullLogger<DurableRealtimeMailboxHub>.Instance))
            {
                await hub.StartAsync(supervisor);
                await hub.CaptureBatchAsync(Enumerable.Range(1, 8).Select(index => new RealtimeSourcePublication(
                    subject with { Verb = index.ToString(System.Globalization.CultureInfo.InvariantCulture) }, Guid.NewGuid(), new byte[] { 1 })).ToArray());
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                while (hub.Metrics.AcknowledgedDeliveries < 8 && !timeout.IsCancellationRequested) await Task.Delay(20);
                Assert.True(hub.Metrics.AcknowledgedDeliveries >= 8, $"Batch transport: {hub.LastFailure}; acknowledged={hub.Metrics.AcknowledgedDeliveries}; published={hub.Metrics.PublishedDestinations}; successful={string.Join(',', succeeded)}");
                Assert.Equal(Enumerable.Range(1, 8), succeeded);
                while (hub.PendingSourceCount > 0) await Task.Delay(20, timeout.Token);
                Volatile.Write(ref holding, true);
                await hub.CaptureBatchAsync(Enumerable.Range(9, 8).Select(index => new RealtimeSourcePublication(
                    subject with { Verb = index.ToString(System.Globalization.CultureInfo.InvariantCulture) }, Guid.NewGuid(), new byte[] { 2 })).ToArray());
                await held.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.True(hub.PendingSourceCount > 0);
            }
            (await held.Task).Dispose(); Volatile.Write(ref holding, false);
            var js = await manager.GetJetStreamContextAsync(connection.Url);
            await js.DeleteStreamAsync(options.StreamPrefix + "_" + options.Epoch + "_" + RealtimeMailboxCodec.ActorKey(subject.Name));
            await using (var hub = new DurableRealtimeMailboxHub(options, connection, manager, NullLogger<DurableRealtimeMailboxHub>.Instance))
            {
                await hub.StartAsync(supervisor);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                while (hub.PendingSourceCount > 0) await Task.Delay(20, timeout.Token);
                Assert.Equal(Enumerable.Range(1, 16), succeeded);
                Assert.Equal(8, hub.Metrics.AcknowledgedDeliveries);
            }
        }
        finally { Directory.Delete(root, true); }
    }

}
