using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

/// <summary>Actual broker ownership with fsynced idempotent recipient fixtures; not a financial-owner qualification.</summary>
public sealed class SourceConfirmationPumpTests
{
    [RealtimeBrokerTheory]
    [InlineData(RealtimeMailboxStorage.Memory)]
    [InlineData(RealtimeMailboxStorage.File)]
    public async Task Handler_proofs_and_physical_ACK_survive_cancelled_full_confirmation_then_cold_changed_route_recovery(RealtimeMailboxStorage storage)
    {
        var fixture = new Fixture(storage); var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var hooks = 0; var physicalAcks = 0;
        try
        {
            await using var manager = new NatsConnectionManager(); IReadOnlyList<RealtimeCaptureReceipt> originals;
            await using (var hub = fixture.Hub(manager))
            {
                hub.BeforePhysicalSettlementAsync = (_, _, ack, _, _) => { if (ack) Interlocked.Increment(ref physicalAcks); return ValueTask.CompletedTask; };
                hub.BeforeSourceConfirmationAsync = async (_, token) =>
                { if (Interlocked.Increment(ref hooks) == 1) reached.TrySetResult(); await release.Task.WaitAsync(token); };
                await hub.StartAsync(fixture.Registry()); originals = await hub.CaptureBatchAsync(fixture.Sources);
                await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
                using var acknowledged = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                while (hub.Metrics.AcknowledgedDeliveries < 6) await Task.Delay(5, acknowledged.Token);
                Assert.True(Volatile.Read(ref hooks) >= 1);
                Assert.Equal(6, hub.Metrics.AcknowledgedDeliveries); Assert.Equal(2, Volatile.Read(ref physicalAcks));
                Assert.Equal(6, fixture.Effects.Count); Assert.Equal(3, hub.PendingSourceCount);
                Assert.Equal(0, new FileInfo(fixture.FullReceipts).Length); Assert.True(new FileInfo(fixture.DeliveryReceipts).Length > 0);
                using var waiter = new CancellationTokenSource(); var drain = hub.WaitForDrainAsync("GLBX.MDP3", fixture.Generation, waiter.Token).AsTask();
                Assert.False(drain.IsCompleted); waiter.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => drain);
            } // Dispose must cancel and await the held confirmation helper; it must not invent a full receipt.
            Assert.Equal(0, new FileInfo(fixture.FullReceipts).Length); Assert.Equal(6, File.ReadAllLines(fixture.EffectFile).Length);
            fixture.ColdRecipientRestart(); fixture.ChangeRoutes = true;
            await using (var recovered = fixture.Hub(manager))
            {
                await recovered.StartAsync(fixture.Registry());
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await recovered.WaitForDrainAsync("GLBX.MDP3", fixture.Generation, timeout.Token);
                Assert.Equal(0, recovered.PendingSourceCount); Assert.Null(recovered.CurrentFailure);
                Assert.Equal(6, fixture.Effects.Count); Assert.Equal(6, fixture.HandlerCalls); Assert.Equal(0, fixture.ChangedRouteCalls);
                Assert.Equal(6, File.ReadAllLines(fixture.EffectFile).Length);
                var fullBytes = new FileInfo(fixture.FullReceipts).Length; Assert.True(fullBytes > 0);
                var duplicate = await recovered.CaptureBatchAsync(fixture.Sources, timeout.Token);
                for (var i = 0; i < originals.Count; i++)
                {
                    Assert.True(duplicate[i].AlreadyCaptured); Assert.Equal(originals[i].SourceEventId, duplicate[i].SourceEventId);
                    Assert.Equal(originals[i].SourceSha256, duplicate[i].SourceSha256); Assert.Equal(originals[i].ManifestSha256, duplicate[i].ManifestSha256);
                }
                Assert.Equal(fullBytes, new FileInfo(fixture.FullReceipts).Length); Assert.Equal(0, recovered.PendingSourceCount);
            }
        }
        finally { release.TrySetResult(); fixture.Delete(); }
    }

    [RealtimeBrokerTheory]
    [InlineData(RealtimeMailboxStorage.Memory)]
    [InlineData(RealtimeMailboxStorage.File)]
    public async Task Full_confirmation_IO_failure_retries_bookkeeping_without_repeating_handler_effects_and_clears_failure(RealtimeMailboxStorage storage)
    {
        var fixture = new Fixture(storage); var failure = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var retry = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0; var failedAt = 0L; var retriedAt = 0L; var first = fixture.Sources[0].SourceEventId;
        try
        {
            await using var manager = new NatsConnectionManager(); await using var hub = fixture.Hub(manager);
            hub.BeforeSourceConfirmationAsync = async (id, token) =>
            {
                if (id != first) return;
                if (Interlocked.Increment(ref attempts) == 1)
                { failedAt = System.Diagnostics.Stopwatch.GetTimestamp(); failure.TrySetResult(); throw new IOException("Injected full-source receipt failure."); }
                retriedAt = System.Diagnostics.Stopwatch.GetTimestamp(); retry.TrySetResult(); await release.Task.WaitAsync(token);
            };
            await hub.StartAsync(fixture.Registry()); await hub.CaptureBatchAsync(fixture.Sources);
            await failure.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (hub.CurrentFailure?.Contains("Injected full-source receipt failure", StringComparison.Ordinal) != true) await Task.Delay(5, timeout.Token);
            Assert.True(hub.PendingSourceCount > 0); Assert.Equal(6, fixture.Effects.Count); Assert.Equal(6, fixture.HandlerCalls);
            Assert.True(Directory.EnumerateFiles(Path.GetDirectoryName(fixture.FullReceipts)!, "*.rto").Any());
            await retry.Task.WaitAsync(timeout.Token); Assert.Equal(2, Volatile.Read(ref attempts));
            Assert.True(System.Diagnostics.Stopwatch.GetElapsedTime(failedAt, retriedAt) >= TimeSpan.FromMilliseconds(200));
            Assert.True(hub.PendingSourceCount > 0); Assert.Equal(6, fixture.HandlerCalls);
            release.TrySetResult(); await hub.WaitForDrainAsync("GLBX.MDP3", fixture.Generation, timeout.Token);
            Assert.Equal(0, hub.PendingSourceCount); Assert.Null(hub.CurrentFailure); Assert.Equal(6, fixture.Effects.Count);
            Assert.Equal(6, fixture.HandlerCalls); Assert.Equal(6, File.ReadAllLines(fixture.EffectFile).Length);
            Assert.True(new FileInfo(fixture.FullReceipts).Length > 0);
        }
        finally { release.TrySetResult(); fixture.Delete(); }
    }

    sealed class Fixture
    {
        readonly string root = Path.Combine(Path.GetTempPath(), "ifm-confirmation-pump", Guid.NewGuid().ToString("N"));
        readonly object effectsGate = new(); HashSet<string> effects = new(StringComparer.Ordinal);
        readonly RealtimeMailboxOptions options;
        readonly NatsProducerOptions connection = new() { Url = Environment.GetEnvironmentVariable("IFM_TEST_REALTIME_NATS")! };
        public readonly Guid Generation = Guid.NewGuid();
        public readonly RealtimeSourcePublication[] Sources;
        public bool ChangeRoutes;
        int handlerCalls, changedRouteCalls;
        public int HandlerCalls => Volatile.Read(ref handlerCalls);
        public int ChangedRouteCalls => Volatile.Read(ref changedRouteCalls);
        public IReadOnlyList<string> Effects { get { lock (effectsGate) return effects.Order(StringComparer.Ordinal).ToArray(); } }
        public string EffectFile => Path.Combine(root, "recipient.effects");
        public string FullReceipts => Path.Combine(options.Directory, options.Epoch, "sources", "receipts.bin");
        public string DeliveryReceipts => Path.Combine(options.Directory, options.Epoch, "sources", "delivery-receipts.bin");
        public Fixture(RealtimeMailboxStorage storage)
        {
            Directory.CreateDirectory(root);
            options = new() { Enabled = true, CapacityQualified = true, Directory = Path.Combine(root, "transport"), Storage = storage,
                Epoch = "confirm_" + Guid.NewGuid().ToString("N"), FailureRedeliveryDelay = TimeSpan.FromMilliseconds(250), DeliveryConcurrency = 2 };
            Sources = Enumerable.Range(1, 3).Select(index => new RealtimeSourcePublication(new(ActorType.Realtime, "PumpOwner", index.ToString(), "ES"), Guid.NewGuid(),
                new byte[] { (byte)index }, Dataset: "GLBX.MDP3", SourceGenerationId: Generation)).ToArray();
        }
        public DurableRealtimeMailboxHub Hub(NatsConnectionManager manager) => new(options, connection, manager, NullLogger<DurableRealtimeMailboxHub>.Instance);
        public IActorSupervisor Registry()
        {
            var supervisor = Substitute.For<IActorSupervisor>(); var children = new Dictionary<ActorMailboxId, IActor>();
            foreach (var name in new[] { "PumpOwner", "PumpOriginalFanout", "PumpChangedFanout" })
            {
                var actor = Substitute.For<IActor, IBatchedRealtimeActor>(); var mailbox = Substitute.For<IActorMailbox>(); var queues = Substitute.For<IActorThreadQueues>();
                actor.Mailbox.Returns(mailbox); mailbox.ThreadQueues.Returns(queues); children.Add(new(ActorType.Realtime, name), actor);
                queues.TryAdmitAsync(Arg.Any<IActorMessage>(), Arg.Any<ActorSubject>(), Arg.Any<CancellationToken>()).Returns(call =>
                {
                    var group = Assert.IsAssignableFrom<IRealtimeBrokerBatch>(call.Arg<IActorMessage>());
                    foreach (var message in group.Messages)
                    {
                        Interlocked.Increment(ref handlerCalls); if (message.Subject.Name == "PumpChangedFanout") Interlocked.Increment(ref changedRouteCalls);
                        var id = Sources[int.Parse(message.SourceSubject.Verb, System.Globalization.CultureInfo.InvariantCulture) - 1].SourceEventId;
                        Apply(message.Subject.Name + "|" + id.ToString("N"));
                        ((IActorDeliveryCompletion)message).CompleteDeliveryAsync(true).GetAwaiter().GetResult();
                    }
                    group.Dispose(); return ValueTask.FromResult(ActorAdmissionResult.AcceptedResult);
                });
            }
            supervisor.Children.Returns(children); supervisor.ActorExists(Arg.Any<ActorMailboxId>()).Returns(true);
            supervisor.GetRealtimeRoutes(Arg.Any<ActorTypeId>()).Returns(_ => ImmutableArray.Create(new RealtimeActorRoute(new(ActorType.Realtime,
                Volatile.Read(ref ChangeRoutes) ? "PumpChangedFanout" : "PumpOriginalFanout"), source => source.EntityId)));
            return supervisor;
        }
        void Apply(string identity)
        {
            lock (effectsGate)
            {
                if (effects.Contains(identity)) return;
                using var file = new FileStream(EffectFile, FileMode.Append, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(file, leaveOpen: true); writer.WriteLine(identity); writer.Flush(); file.Flush(true); effects.Add(identity);
            }
        }
        public void ColdRecipientRestart() { lock (effectsGate) effects = new(File.ReadAllLines(EffectFile), StringComparer.Ordinal); }
        public void Delete() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
