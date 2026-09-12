using System;
using System.IO;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventSourcing;

public sealed class EventLogBinaryCodecTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RoundTrip_preserves_business_identity_null_metadata_and_storage_version(bool compressed)
    {
        var codec = new EventLogMessagePackCodec(compressed);
        var source = new UnknownEvent(default, Guid.NewGuid(), default, 0, Guid.NewGuid(),
            "", "test", DateTime.UtcNow, 3, 4, "missing", "diagnostic", DateTime.UtcNow)
            { AggregateId = null! };
        var bytes = codec.Serialize(source);
        var result = Assert.IsType<UnknownEvent>(codec.Deserialize(source.GetType().AssemblyQualifiedName!, 99, bytes));
        Assert.Equal(source with { EventId = 99 }, result);
        Assert.Null(result.AggregateId);
        if (!compressed) Assert.Equal(result, new EventStreamReadModel { EventTypeName = source.GetType().AssemblyQualifiedName!,
            EventVersion = 99, EventData = bytes }.ToDomainEvent());
    }

    [Fact]
    public void Known_event_corruption_fails_instead_of_silently_skipping_replay()
    {
        var row = new EventStreamReadModel { EventTypeName = typeof(UnknownEvent).AssemblyQualifiedName!,
            EventVersion = 99, EventData = [123, 125] };
        Assert.ThrowsAny<Exception>(() => row.ToDomainEvent());
    }

    [Fact]
    public void Non_event_registered_type_is_rejected()
        => Assert.Throws<InvalidDataException>(() => EventLogMessagePackCodec.Shared.Deserialize(typeof(string).AssemblyQualifiedName!, 1, [1]));

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Payload_is_replayable_when_reader_compression_preference_changes(bool writtenCompressed, bool readCompressed)
    {
        var source = new UnknownEvent(default, Guid.NewGuid(), default, 0, Guid.NewGuid(),
            "", "test", DateTime.UtcNow, 3, 4, "missing", "diagnostic", DateTime.UtcNow);
        var payload = new EventLogMessagePackCodec(writtenCompressed).Serialize(source);

        var replayed = new EventLogMessagePackCodec(readCompressed)
            .Deserialize(source.GetType().AssemblyQualifiedName!, 12, payload);

        Assert.Equal(source with { EventId = 12 }, replayed);
    }
}
