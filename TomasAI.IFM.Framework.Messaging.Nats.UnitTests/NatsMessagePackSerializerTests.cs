using System.Buffers;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class NatsMessagePackSerializerTests
{
    [Fact]
    public void Round_trip_writes_directly_to_buffer_writer()
    {
        var serializer = NatsMessagePackSerializer<Payload>.Default;
        var expected = new Payload(Guid.NewGuid(), "fund.created", [1, 2, 3, 4]);
        var writer = new ArrayBufferWriter<byte>();

        serializer.Serialize(writer, expected);
        var sequence = new ReadOnlySequence<byte>(writer.WrittenMemory);
        var actual = serializer.Deserialize(sequence);

        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    [Trait("Category", "Verification")]
    public void Nats_and_binary_boundaries_share_compression_and_decode_each_others_payloads()
    {
        var expected = new Payload(Guid.NewGuid(), "selection.completed", Enumerable.Repeat((byte)7, 10000).ToArray());
        var binary = Serialization.MessagePackBinarySerializer.Shared;
        var writer = new ArrayBufferWriter<byte>();
        var nats = NatsMessagePackSerializer<Payload>.Default;
        nats.Serialize(writer, expected);
        writer.WrittenSpan.ToArray().Should().Equal(binary.Serialize(expected)!);
        binary.Deserialize<Payload>(writer.WrittenMemory).Should().BeEquivalentTo(expected);
        nats.Deserialize(new ReadOnlySequence<byte>(binary.Serialize(expected)!)).Should().BeEquivalentTo(expected);
        Serialization.MessagePackBinarySerializer.MeasureEncoded(expected).Should().Be(writer.WrittenCount);
        Serialization.MessagePackBinarySerializer.MeasureContent(expected).Should().BeGreaterThan(writer.WrittenCount);
    }

    [Fact]
    [Trait("Category", "Verification")]
    public void Reused_writer_serialization_has_no_per_message_intermediate_payload_allocation()
    {
        var serializer = NatsMessagePackSerializer<Payload>.Default;
        var payload = new Payload(
            Guid.Parse("55e8981e-0c81-4fc6-8f87-79d155776707"),
            "allocation.verify",
            Enumerable.Repeat((byte)7, 4096).ToArray());
        var writer = new FixedBufferWriter(8192);

        for (var index = 0; index < 20; index++)
        {
            writer.Reset();
            serializer.Serialize(writer, payload);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1_000; index++)
        {
            writer.Reset();
            serializer.Serialize(writer, payload);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Runtime/Meter bookkeeping can contribute one fixed sub-128-byte allocation to
        // the complete loop. A byte[] regression would add roughly 4 KB on every iteration.
        allocated.Should().BeLessThanOrEqualTo(128);
    }

    sealed class FixedBufferWriter(int capacity) : IBufferWriter<byte>
    {
        readonly byte[] _buffer = GC.AllocateUninitializedArray<byte>(capacity);
        int _written;

        public void Advance(int count) => _written += count;
        public Memory<byte> GetMemory(int sizeHint = 0) => _buffer.AsMemory(_written);
        public Span<byte> GetSpan(int sizeHint = 0) => _buffer.AsSpan(_written);
        public void Reset() => _written = 0;
    }

    public sealed record Payload(Guid Id, string Subject, byte[] Data);
}
