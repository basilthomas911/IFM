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

    public sealed record Payload(Guid Id, string Subject, byte[] Data);
}
