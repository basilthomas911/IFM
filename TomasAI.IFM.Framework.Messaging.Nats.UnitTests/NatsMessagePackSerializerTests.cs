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

    public sealed record Payload(Guid Id, string Subject, byte[] Data);
}
