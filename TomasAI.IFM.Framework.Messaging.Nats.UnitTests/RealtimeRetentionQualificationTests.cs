using System.Buffers.Binary;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class RealtimeRetentionQualificationTests
{
    static readonly ActorSubject Target = new(ActorType.Realtime, "Retention", "Updated", "ES");
    static readonly string Mailbox = RealtimeMailboxCodec.MailboxKey(Target.ThreadId);
    static CapturedRealtimeSource Source(int index, byte value = 1) => RealtimeMailboxCodec.Capture(
        new(Target, new Guid(index + 1, 0, 0, new byte[8]), new byte[] { value }), [Target], 1024);
    static RealtimeMailboxOptions Options(string root) => new() { Directory = root, MaximumPayloadBytes = 1024, CaptureMaximumDelay = TimeSpan.Zero };
    static async Task Finish(RealtimeSourceOutbox store, CapturedRealtimeSource source)
    {
        await store.RecordDestinationCompletedAsync(source, Mailbox, default);
        await store.ConfirmBrokerOwnershipAsync(source, default);
    }
    [Fact]
    public async Task Disk_capacity_refuses_new_sources_but_preserves_pinned_frames_and_recovers_after_proven_drain()
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm-retention-cap-" + Guid.NewGuid().ToString("N"));
        var options = Options(root);
        options.MaximumOutboxBytes = 16L * 1024 * 1024 + (1024 + 65536L) * options.CaptureBatchSize + 2 * 65537L + 4096;
        var first = Source(1); var second = Source(2); var refused = Source(3);
        try
        {
            await using (var store = new RealtimeSourceOutbox(options))
            {
                await store.CaptureBatchAsync(new[] { first, second }, default);
                Assert.Equal(Directory.EnumerateFiles(root, "*.rto", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length), store.RetainedSegmentBytes);
                await Assert.ThrowsAsync<IOException>(async () => await store.CaptureAsync(refused, default));
                Assert.NotNull(store.CapacityFailure); Assert.Null(store.Failure);
                await Finish(store, first);
                Assert.True(store.RetainedSegmentBytes > 0); // second still pins the original segment
                Assert.Equal(second.Key, Assert.Single(store.Pending()).Key);
                await store.CaptureAsync(refused, default);
                await Finish(store, second); await Finish(store, refused);
                Assert.Empty(store.Pending()); Assert.Equal(0, store.RetainedSegmentBytes); Assert.Null(store.CapacityFailure);
                Assert.Equal(0, Directory.EnumerateFiles(root, "*.rto", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length));
            }
            await using (var restarted = new RealtimeSourceOutbox(options))
            {
                Assert.True((await restarted.CaptureAsync(first, default)).AlreadyCaptured);
                await Assert.ThrowsAsync<InvalidDataException>(async () => await restarted.CaptureAsync(Source(1, 2), default));
                await restarted.CaptureAsync(Source(4), default); await Finish(restarted, Source(4));
                Assert.Null(restarted.CapacityFailure); Assert.Equal(0, restarted.RetainedSegmentBytes);
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Receipt_horizon_exhaustion_remains_visible_after_restart_without_blocking_owned_settlement_or_duplicate_proofs()
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm-retention-receipts-" + Guid.NewGuid().ToString("N"));
        var options = Options(root); options.MaximumReceiptBytes = 296;
        var first = Source(1); var second = Source(2); var third = Source(3);
        try
        {
            await using (var store = new RealtimeSourceOutbox(options))
            {
                await store.CaptureBatchAsync(new[] { first, second }, default);
                Assert.NotNull(store.CapacityFailure);
                await Finish(store, first); await Finish(store, second);
                Assert.Empty(store.Pending()); Assert.Null(store.Failure);
                Assert.Contains("horizon", store.CapacityFailure);
                Assert.True((await store.CaptureAsync(first, default)).AlreadyCaptured);
                await Assert.ThrowsAsync<IOException>(async () => await store.CaptureAsync(third, default));
            }
            await using (var restarted = new RealtimeSourceOutbox(options))
            {
                Assert.Contains("horizon", restarted.CapacityFailure);
                Assert.True(restarted.IsDestinationCompleted(first, Mailbox));
                Assert.True((await restarted.CaptureAsync(second, default)).AlreadyCaptured);
            }
            options.MaximumReceiptBytes = 444; // explicit capacity expansion, never time-based proof deletion
            await using (var expanded = new RealtimeSourceOutbox(options))
            {
                Assert.Null(expanded.CapacityFailure);
                await expanded.CaptureAsync(third, default); await Finish(expanded, third);
                Assert.Contains("horizon", expanded.CapacityFailure);
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Twenty_thousand_actual_source_receipts_survive_hot_cache_eviction_and_cold_archive_index_rebuild()
    {
        const int count = 20000;
        var root = Path.Combine(Path.GetTempPath(), "ifm-retention-history-" + Guid.NewGuid().ToString("N"));
        var options = Options(root); options.MaximumReceiptBytes = count * 148L;
        var sources = Enumerable.Range(0, count).Select(index => Source(index, (byte)(index % 251 + 1))).ToArray();
        var colliding = sources.GroupBy(source => BinaryPrimitives.ReadUInt32LittleEndian(Convert.FromHexString(source.Key)) & ((1 << 20) - 1))
            .First(group => group.Count() > 1).ToArray();
        try
        {
            await using (var store = new RealtimeSourceOutbox(options))
            {
                foreach (var group in sources.Chunk(64))
                {
                    await store.CaptureBatchAsync(group, default);
                    await Task.WhenAll(group.Select(source => store.RecordDestinationCompletedAsync(source, Mailbox, default).AsTask()));
                    await Task.WhenAll(group.Select(source => store.ConfirmBrokerOwnershipAsync(source, default).AsTask()));
                    Assert.InRange(store.RetainedSegmentBytes, 0, options.MaximumOutboxBytes);
                }
                Assert.Empty(store.Pending()); Assert.Contains("horizon", store.CapacityFailure);
                Assert.True((await store.CaptureAsync(sources[0], default)).AlreadyCaptured);
            }
            await using (var restarted = new RealtimeSourceOutbox(options))
            {
                foreach (var index in new[] { 0, 8000, 10000, count - 1 })
                {
                    Assert.True((await restarted.CaptureAsync(sources[index], default)).AlreadyCaptured);
                    Assert.True(restarted.IsDestinationCompleted(sources[index], Mailbox));
                }
                foreach (var source in colliding) Assert.True((await restarted.CaptureAsync(source, default)).AlreadyCaptured);
                Assert.Empty(restarted.Pending()); Assert.Equal(0, restarted.RetainedSegmentBytes);
                await Assert.ThrowsAsync<IOException>(async () => await restarted.CaptureAsync(Source(count), default));
                await Assert.ThrowsAsync<InvalidDataException>(async () => await restarted.CaptureAsync(Source(0, 254), default));
            }
        }
        finally { Directory.Delete(root, true); }
    }
}
