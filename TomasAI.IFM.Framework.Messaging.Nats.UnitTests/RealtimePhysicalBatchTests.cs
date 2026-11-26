using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

/// <summary>Actual disposable broker plus a durable idempotent recipient fixture; not a financial-owner qualification.</summary>
public sealed class RealtimePhysicalBatchTests
{
    [RealtimeBrokerTheory]
    [InlineData(RealtimeMailboxStorage.Memory, false)]
    [InlineData(RealtimeMailboxStorage.File, false)]
    [InlineData(RealtimeMailboxStorage.Memory, true)]
    [InlineData(RealtimeMailboxStorage.File, true)]
    public async Task Partial_member_failure_retains_one_physical_group_and_cold_replay_preserves_frozen_fanout_without_duplicate_effects(RealtimeMailboxStorage storage, bool mirrorFails)
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm-physical-batch-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var source = new ActorSubject(ActorType.Realtime, "PhysicalBatchOwner", "1", "ES");
        var options = new RealtimeMailboxOptions { Enabled = true, CapacityQualified = true, Storage = storage, Directory = Path.Combine(root, "transport"),
            Epoch = "physical_" + Guid.NewGuid().ToString("N"), FailureRedeliveryDelay = TimeSpan.FromSeconds(5), DeliveryConcurrency = 2 };
        var connection = new NatsProducerOptions { Url = Environment.GetEnvironmentVariable("IFM_TEST_REALTIME_NATS")! };
        var originalSources = Enumerable.Range(1, 3).Select(value => new RealtimeSourcePublication(source with { Verb = value.ToString() }, Guid.NewGuid(), new byte[] { (byte)value })).ToArray();
        var lifecycle = new ConcurrentQueue<(string Actor, bool Acknowledged)>(); var attempts = new ConcurrentQueue<(string Actor, int[] Members)>();
        var failing = true; var changedRoutes = false; var ledger = new RecipientLedger(Path.Combine(root, "owner.effects"));
        IActorSupervisor Registry()
        {
            var registry = Substitute.For<IActorSupervisor>(); var children = new Dictionary<ActorMailboxId, IActor>();
            foreach (var name in new[] { source.Name, "PhysicalFrozenMirror", "PhysicalChangedMirror" })
            {
                var actor = Substitute.For<IActor, IBatchedRealtimeActor>(); var mailbox = Substitute.For<IActorMailbox>(); var queues = Substitute.For<IActorThreadQueues>();
                actor.Mailbox.Returns(mailbox); mailbox.ThreadQueues.Returns(queues); children.Add(new(ActorType.Realtime, name), actor);
                queues.TryAdmitAsync(Arg.Any<IActorMessage>(), Arg.Any<ActorSubject>(), Arg.Any<CancellationToken>()).Returns(call =>
                {
                    var group = Assert.IsAssignableFrom<IRealtimeBrokerBatch>(call.Arg<IActorMessage>());
                    var targetName = group.Subject.Name;
                    var values = group.Messages.Select(message => int.Parse(message.SourceSubject.Verb, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    attempts.Enqueue((targetName, values));
                    var blocked = false;
                    foreach (var message in group.Messages)
                    {
                        var index = int.Parse(message.SourceSubject.Verb, System.Globalization.CultureInfo.InvariantCulture);
                        if (Volatile.Read(ref failing) && (targetName == source.Name || mirrorFails) && index == 2) blocked = true;
                        if (!blocked) ledger.Apply(targetName + ":" + index);
                        ((IActorDeliveryCompletion)message).CompleteDeliveryAsync(!blocked).GetAwaiter().GetResult();
                    }
                    group.Dispose(); return ValueTask.FromResult(ActorAdmissionResult.AcceptedResult);
                });
            }
            registry.Children.Returns(children); registry.ActorExists(Arg.Any<ActorMailboxId>()).Returns(true);
            registry.GetRealtimeRoutes(Arg.Any<ActorTypeId>()).Returns(_ => ImmutableArray.Create(new RealtimeActorRoute(
                new(ActorType.Realtime, Volatile.Read(ref changedRoutes) ? "PhysicalChangedMirror" : "PhysicalFrozenMirror"), target => target.EntityId)));
            return registry;
        }
        try
        {
            await using var manager = new NatsConnectionManager(); var js = await manager.GetJetStreamContextAsync(connection.Url);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await using (var hub = new DurableRealtimeMailboxHub(options, connection, manager, NullLogger<DurableRealtimeMailboxHub>.Instance))
            {
                hub.BeforePhysicalSettlementAsync = (identity, _, acknowledge, _, _) => { lifecycle.Enqueue((identity.Name, acknowledge)); return ValueTask.CompletedTask; };
                await hub.StartAsync(Registry()); await hub.CaptureBatchAsync(originalSources, deadline.Token);
                var observation = System.Diagnostics.Stopwatch.StartNew();
                while (hub.Metrics.FailedDeliveries < (mirrorFails ? 4 : 2) || hub.Metrics.AcknowledgedDeliveries < (mirrorFails ? 0 : 3))
                {
                    Assert.True(observation.Elapsed < TimeSpan.FromSeconds(8), $"group pending={hub.PendingSourceCount}, published={hub.Metrics.PublishedDestinations}, ack={hub.Metrics.AcknowledgedDeliveries}, failed={hub.Metrics.FailedDeliveries}, error={hub.LastFailure}, current={hub.CurrentFailure}, attempts={string.Join(';', attempts.Select(item => item.Actor + ':' + string.Join(',', item.Members)))}");
                    await Task.Delay(10, deadline.Token);
                }
                Assert.Contains(attempts, attempt => attempt.Actor == source.Name && attempt.Members.SequenceEqual(new[] { 1, 2, 3 }));
                Assert.DoesNotContain(lifecycle, result => result.Actor == source.Name && result.Acknowledged);
                Assert.Contains(lifecycle, result => result.Actor == source.Name && !result.Acknowledged);
                Assert.Equal(new[] { source.Name + ":1" }, ledger.Keys.Where(key => key.StartsWith(source.Name + ":", StringComparison.Ordinal)));
                Assert.Equal(3, hub.PendingSourceCount);
                var stream = await js.GetStreamAsync(Stream(options, source.Name), cancellationToken: deadline.Token);
                Assert.Equal(1L, stream.Info.State.Messages); Assert.Equal(1UL, stream.Info.State.LastSeq);
            }
            // Simulate a cold process and broker-state loss. The source journal and recipient's own receipts survive.
            ledger = new RecipientLedger(Path.Combine(root, "owner.effects")); Volatile.Write(ref failing, false); Volatile.Write(ref changedRoutes, true);
            await js.DeleteStreamAsync(Stream(options, source.Name), deadline.Token);
            await js.DeleteStreamAsync(Stream(options, "PhysicalFrozenMirror"), deadline.Token);
            await using (var restarted = new DurableRealtimeMailboxHub(options, connection, manager, NullLogger<DurableRealtimeMailboxHub>.Instance))
            {
                await restarted.StartAsync(Registry());
                while (restarted.PendingSourceCount != 0) await Task.Delay(10, deadline.Token);
                Assert.Equal(Enumerable.Range(1, 3).Select(index => source.Name + ":" + index), ledger.Keys.Where(key => key.StartsWith(source.Name + ":", StringComparison.Ordinal)));
                Assert.Equal(6, ledger.Keys.Count); Assert.Equal(6, File.ReadAllLines(Path.Combine(root, "owner.effects")).Length);
                Assert.DoesNotContain(attempts, attempt => attempt.Actor == "PhysicalChangedMirror");
                Assert.Equal(mirrorFails ? 6 : 3, restarted.Metrics.AcknowledgedDeliveries);
                if (mirrorFails) Assert.Contains(attempts.Skip(2), attempt => attempt.Actor == "PhysicalFrozenMirror" && attempt.Members.SequenceEqual(new[] { 1, 2, 3 }));
                Assert.Contains(attempts.Skip(2), attempt => attempt.Actor == source.Name && attempt.Members.SequenceEqual(new[] { 1, 2, 3 }));
            }
        }
        finally { Directory.Delete(root, true); }
    }

    static string Stream(RealtimeMailboxOptions options, string actor) => options.StreamPrefix + "_" + options.Epoch + "_" + RealtimeMailboxCodec.ActorKey(actor);

    sealed class RecipientLedger(string path)
    {
        readonly object gate = new();
        readonly HashSet<string> applied = File.Exists(path) ? new(File.ReadAllLines(path), StringComparer.Ordinal) : new(StringComparer.Ordinal);
        public IReadOnlyList<string> Keys { get { lock (gate) return applied.Order(StringComparer.Ordinal).ToArray(); } }
        public void Apply(string identity)
        {
            lock (gate)
            {
                if (applied.Contains(identity)) return;
                using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream, leaveOpen: true); writer.WriteLine(identity); writer.Flush(); stream.Flush(true);
                applied.Add(identity);
            }
        }
    }
}
