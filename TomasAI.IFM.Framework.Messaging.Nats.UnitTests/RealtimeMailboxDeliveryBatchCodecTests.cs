using System.Buffers.Binary;
using System.Security.Cryptography;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class RealtimeMailboxDeliveryBatchCodecTests
{
    static byte[] Delivery(int value)
    {
        var subject = new ActorSubject(ActorType.Realtime, "BrokerBatchTest", "Changed", value.ToString());
        return RealtimeMailboxCodec.Encode(RealtimeMailboxCodec.Capture(new(subject, Guid.NewGuid(), new byte[] { (byte)value, 20, 30 }), [subject], 4096));
    }
    static void Resign(byte[] wire) => SHA256.HashData(wire.AsSpan(0, wire.Length - 32), wire.AsSpan(wire.Length - 32));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(64)]
    public void Roundtrip_preserves_original_inner_bytes_order_and_source_proofs(int count)
    {
        var deliveries = Enumerable.Range(1, count).Select(Delivery).ToArray();
        var wire = RealtimeMailboxDeliveryBatchCodec.Encode(deliveries, 65536);
        var restored = RealtimeMailboxDeliveryBatchCodec.Decode(wire, wire.Length);
        Assert.Equal(count, restored.Count);
        for (var i = 0; i < count; i++)
        {
            Assert.Equal(deliveries[i], restored[i]); Assert.NotSame(deliveries[i], restored[i]);
            Assert.Equal(RealtimeMailboxCodec.Decode(deliveries[i], 4096).SourceHash, RealtimeMailboxCodec.Decode(restored[i], 4096).SourceHash);
        }
    }

    [Fact]
    public void Legacy_delivery_is_the_exact_same_singleton_wire_without_reserialization()
    {
        var wire = Delivery(1); var result = RealtimeMailboxDeliveryBatchCodec.Decode(wire, wire.Length);
        Assert.Single(result); Assert.Same(wire, result[0]);
        Assert.Throws<InvalidDataException>(() => RealtimeMailboxDeliveryBatchCodec.Decode(wire, wire.Length - 1));
    }

    [Fact]
    public void Total_byte_bound_includes_all_headers_member_lengths_and_digest()
    {
        var deliveries = new[] { Delivery(1), Delivery(2) };
        var size = 16 + 32 + deliveries.Sum(value => sizeof(int) + value.Length);
        var wire = RealtimeMailboxDeliveryBatchCodec.Encode(deliveries, size); Assert.Equal(size, wire.Length);
        Assert.Throws<InvalidDataException>(() => RealtimeMailboxDeliveryBatchCodec.Encode(deliveries, size - 1));
        Assert.Throws<InvalidDataException>(() => RealtimeMailboxDeliveryBatchCodec.Decode(wire, size - 1));
        Assert.Throws<InvalidDataException>(() => RealtimeMailboxDeliveryBatchCodec.Encode(deliveries, 0));
    }

    [Fact]
    public void Every_single_byte_tamper_and_every_truncation_is_rejected_before_any_member_is_returned()
    {
        var wire = RealtimeMailboxDeliveryBatchCodec.Encode([Delivery(1), Delivery(2)], 65536);
        for (var i = 0; i < wire.Length; i++)
        {
            var changed = wire.ToArray(); changed[i] ^= 1;
            Assert.Throws<InvalidDataException>(() => RealtimeMailboxDeliveryBatchCodec.Decode(changed, 65536));
        }
        for (var size = 0; size < wire.Length; size++)
            Assert.Throws<InvalidDataException>(() => RealtimeMailboxDeliveryBatchCodec.Decode(wire[..size], 65536));
    }

    [Fact]
    public void Invalid_counts_inner_versions_lengths_and_trailing_bytes_fail_even_with_matching_outer_digest()
    {
        Assert.Throws<InvalidDataException>(() => RealtimeMailboxDeliveryBatchCodec.Encode([], 65536));
        Assert.Throws<InvalidDataException>(() => RealtimeMailboxDeliveryBatchCodec.Encode(Enumerable.Range(1, 65).Select(Delivery).ToArray(), 65536));
        Assert.Throws<InvalidDataException>(() => RealtimeMailboxDeliveryBatchCodec.Encode([new byte[] { 3, 0, 0, 0 }], 65536));
        foreach (var count in new[] { -1, 0, 65, int.MaxValue })
        {
            var wire = RealtimeMailboxDeliveryBatchCodec.Encode([Delivery(1)], 65536);
            BinaryPrimitives.WriteInt32LittleEndian(wire.AsSpan(12), count); Resign(wire);
            Assert.Throws<InvalidDataException>(() => RealtimeMailboxDeliveryBatchCodec.Decode(wire, 65536));
        }
        foreach (var length in new[] { -1, 0, 3, int.MaxValue })
        {
            var wire = RealtimeMailboxDeliveryBatchCodec.Encode([Delivery(1)], 65536);
            BinaryPrimitives.WriteInt32LittleEndian(wire.AsSpan(16), length); Resign(wire);
            Assert.Throws<InvalidDataException>(() => RealtimeMailboxDeliveryBatchCodec.Decode(wire, 65536));
        }
        var unknown = RealtimeMailboxDeliveryBatchCodec.Encode([Delivery(1)], 65536);
        BinaryPrimitives.WriteInt32LittleEndian(unknown.AsSpan(8), 2); Resign(unknown);
        Assert.Throws<InvalidDataException>(() => RealtimeMailboxDeliveryBatchCodec.Decode(unknown, 65536));
        var wrongInner = RealtimeMailboxDeliveryBatchCodec.Encode([Delivery(1)], 65536);
        BinaryPrimitives.WriteInt32LittleEndian(wrongInner.AsSpan(20), 3); Resign(wrongInner);
        Assert.Throws<InvalidDataException>(() => RealtimeMailboxDeliveryBatchCodec.Decode(wrongInner, 65536));
        var original = RealtimeMailboxDeliveryBatchCodec.Encode([Delivery(1)], 65536);
        var trailing = new byte[original.Length + 1]; original.AsSpan(0, original.Length - 32).CopyTo(trailing); Resign(trailing);
        Assert.Throws<InvalidDataException>(() => RealtimeMailboxDeliveryBatchCodec.Decode(trailing, 65536));
    }
}
