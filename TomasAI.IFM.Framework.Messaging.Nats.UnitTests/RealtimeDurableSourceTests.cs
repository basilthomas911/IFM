using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class RealtimeDurableSourceTests
{
    static readonly ActorSubject Subject = new(ActorType.Realtime, "TradePrice", "Updated", "ES20261218");
    static CapturedRealtimeSource Source(Guid? id = null, byte value = 1) => RealtimeMailboxCodec.Capture(
        new(Subject, id ?? Guid.NewGuid(), new byte[] { value }), [Subject], 1024);
    static RealtimeMailboxOptions Options(string root) => new() { Directory = root, CaptureMaximumDelay = TimeSpan.Zero, MaximumPayloadBytes = 1024 };

    [Fact]
    public void FrozenFanoutAndIdentitySurviveCodecRoundTrip()
    {
        var source = Source();
        var decoded = RealtimeMailboxCodec.Decode(RealtimeMailboxCodec.Delivery(source, Subject), 1024);
        Assert.Equal(source.Key, decoded.Key);
        Assert.Equal(source.SourceHash, decoded.SourceHash);
        Assert.Equal(source.ManifestHash, decoded.ManifestHash);
        Assert.Equal(source.Destinations, decoded.Destinations);
        var changed = Source(source.Source.SourceEventId, 2);
        Assert.Equal(source.Key, changed.Key);
        Assert.NotEqual(source.SourceHash, changed.SourceHash);
    }

    [Fact]
    public async Task PayloadReleaseDoesNotAcknowledgeActiveHandler()
    {
        using var message = new RealtimeBrokerActorMessage(Source(), Subject);
        message.ReleasePayload();
        Assert.False(message.Completion.IsCompleted);
        await message.CompleteDeliveryAsync(true);
        Assert.Equal(BrokerDeliveryResult.Succeeded, await message.Completion);
    }

    [Fact]
    public async Task FlushedPendingSourcesRecoverInCaptureOrderAndReceiptsDeduplicate()
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm-mailbox-test-" + Guid.NewGuid().ToString("N"));
        var options = Options(root); var first = Source(); var second = Source();
        try
        {
            await using (var store = new RealtimeSourceOutbox(options))
            {
                await store.CaptureAsync(first, default); await store.CaptureAsync(second, default);
                Assert.Equal(new[] { first.Key, second.Key }, store.Pending().Select(v => v.Key));
            }
            await using (var store = new RealtimeSourceOutbox(options))
            {
                Assert.Equal(new[] { first.Key, second.Key }, store.Pending().Select(v => v.Key));
                await store.RecordDestinationCompletedAsync(first, RealtimeMailboxCodec.MailboxKey(Subject.ThreadId), default);
                await store.ConfirmBrokerOwnershipAsync(first, default);
                await store.RecordDestinationCompletedAsync(second, RealtimeMailboxCodec.MailboxKey(Subject.ThreadId), default);
                await store.ConfirmBrokerOwnershipAsync(second, default);
            }
            await using (var store = new RealtimeSourceOutbox(options))
            {
                Assert.Empty(store.Pending());
                Assert.True((await store.CaptureAsync(first, default)).AlreadyCaptured);
                await Assert.ThrowsAsync<InvalidDataException>(async () => await store.CaptureAsync(Source(first.Source.SourceEventId, 2), default));
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task PartialFanoutCompletionSurvivesRestartBeforeOtherRecipient()
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm-mailbox-test-" + Guid.NewGuid().ToString("N"));
        var options = Options(root); var other = Subject with { Name = "Indicator" };
        var source = RealtimeMailboxCodec.Capture(new(Subject, Guid.NewGuid(), new byte[] { 1 }), [Subject, other], 1024);
        var primaryKey = RealtimeMailboxCodec.MailboxKey(Subject.ThreadId); var otherKey = RealtimeMailboxCodec.MailboxKey(other.ThreadId);
        try
        {
            await using (var store = new RealtimeSourceOutbox(options))
            {
                await store.CaptureAsync(source, default);
                await store.RecordDestinationCompletedAsync(source, primaryKey, default);
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.ConfirmBrokerOwnershipAsync(source, default));
            }
            await using (var store = new RealtimeSourceOutbox(options))
            {
                Assert.True(store.IsDestinationCompleted(source, primaryKey));
                Assert.False(store.IsDestinationCompleted(source, otherKey));
                Assert.Single(store.Pending());
                await store.RecordDestinationCompletedAsync(source, otherKey, default);
                await store.ConfirmBrokerOwnershipAsync(source, default);
            }
            await using (var store = new RealtimeSourceOutbox(options))
            {
                Assert.True(store.IsDestinationCompleted(source, primaryKey));
                Assert.True(store.IsDestinationCompleted(source, otherKey));
                Assert.Empty(store.Pending());
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task BatchCapacityAndChangedIdentityRefuseWholeNewGroupWithoutOvertaking()
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm-mailbox-test-" + Guid.NewGuid().ToString("N"));
        var options = Options(root); options.MaximumPendingSources = 2;
        try
        {
            await using var store = new RealtimeSourceOutbox(options);
            var first = Source(); var second = Source(); var third = Source();
            await store.CaptureAsync(first, default);
            await Assert.ThrowsAsync<IOException>(async () => await store.CaptureBatchAsync(new[] { second, third }, default));
            Assert.Equal(first.Key, Assert.Single(store.Pending()).Key);
            await Assert.ThrowsAsync<InvalidDataException>(async () => await store.CaptureBatchAsync(new[] { second, Source(first.Source.SourceEventId, 2) }, default));
            Assert.Equal(first.Key, Assert.Single(store.Pending()).Key);
            var receipts = await store.CaptureBatchAsync(new[] { first, second }, default);
            Assert.True(receipts[0].AlreadyCaptured); Assert.False(receipts[1].AlreadyCaptured);
            Assert.Equal(new[] { first.Key, second.Key }, store.Pending().Select(value => value.Key));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task DestinationProofCompactionKeepsPartialSourceAndUsesCompletedSourceAuthority()
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm-mailbox-test-" + Guid.NewGuid().ToString("N"));
        var options = Options(root); options.MaximumReceiptBytes = 1024;
        var other = Subject with { Name = "Indicator" };
        var primaryKey = RealtimeMailboxCodec.MailboxKey(Subject.ThreadId); var otherKey = RealtimeMailboxCodec.MailboxKey(other.ThreadId);
        CapturedRealtimeSource Make() => RealtimeMailboxCodec.Capture(new(Subject, Guid.NewGuid(), new byte[] { 1 }), [Subject, other], 1024);
        var completed = Make(); var partial = Make();
        try
        {
            await using (var store = new RealtimeSourceOutbox(options))
            {
                for (var index = 0; index < 4; index++)
                {
                    var source = index == 0 ? completed : Make(); await store.CaptureAsync(source, default);
                    await store.RecordDestinationCompletedAsync(source, primaryKey, default);
                    await store.RecordDestinationCompletedAsync(source, otherKey, default);
                    await store.ConfirmBrokerOwnershipAsync(source, default);
                }
                var before = store.ReceiptBytes;
                await store.CaptureAsync(partial, default); await store.RecordDestinationCompletedAsync(partial, primaryKey, default);
                Assert.True(store.ReceiptBytes < before);
            }
            await using (var store = new RealtimeSourceOutbox(options))
            {
                Assert.True(store.IsDestinationCompleted(completed, primaryKey));
                Assert.True(store.IsDestinationCompleted(completed, otherKey));
                Assert.True(store.IsDestinationCompleted(partial, primaryKey));
                Assert.False(store.IsDestinationCompleted(partial, otherKey));
                Assert.Equal(partial.Key, Assert.Single(store.Pending()).Key);
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task CapacityRefusesNewSourceWithoutEvictingPending()
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm-mailbox-test-" + Guid.NewGuid().ToString("N"));
        var options = Options(root); options.MaximumPendingSources = 1;
        try
        {
            await using var store = new RealtimeSourceOutbox(options);
            var first = Source(); await store.CaptureAsync(first, default);
            await Assert.ThrowsAsync<IOException>(async () => await store.CaptureAsync(Source(), default));
            Assert.Equal(first.Key, Assert.Single(store.Pending()).Key);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task TornUncommittedTailIsDiscardedButCommittedCorruptionBlocksRecovery()
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm-mailbox-test-" + Guid.NewGuid().ToString("N"));
        var options = Options(root); var first = Source();
        try
        {
            await using (var store = new RealtimeSourceOutbox(options)) await store.CaptureAsync(first, default);
            var path = Directory.GetFiles(Path.Combine(root, options.Epoch, "sources"), "*.rto").First();
            using (var tail = new FileStream(path, FileMode.Append)) tail.Write(new byte[] { 1, 2 });
            await using (var store = new RealtimeSourceOutbox(options)) Assert.Single(store.Pending());
            var bytes = File.ReadAllBytes(path); bytes[40] ^= 1; File.WriteAllBytes(path, bytes);
            Assert.Throws<InvalidDataException>(() => new RealtimeSourceOutbox(options));
        }
        finally { Directory.Delete(root, true); }
    }
}
