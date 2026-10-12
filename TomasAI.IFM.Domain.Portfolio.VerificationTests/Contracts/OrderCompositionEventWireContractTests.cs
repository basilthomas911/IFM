using System.Reflection;
using FluentAssertions;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Domain.Portfolio.VerificationTests.Contracts;

/// <summary>Freezes the published numeric schemas of Order Composition terminal events.</summary>
public sealed class OrderCompositionEventWireContractTests
{
    /// <summary>Gets each terminal event type and its published number of keys.</summary>
    public static TheoryData<Type, int> Contracts => new()
    {
        { typeof(PortfolioOrderCompositionCompletedEvent), 16 },
        { typeof(PortfolioOrderCompositionFailedEvent), 15 },
        { typeof(PortfolioCloseOrderCompositionCompletedEvent), 17 },
        { typeof(PortfolioCloseOrderCompositionFailedEvent), 15 },
    };

    /// <summary>Verifies the direct schema remains contiguous and survives the standard serializer.</summary>
    [Theory, MemberData(nameof(Contracts))]
    public void Terminal_events_preserve_numeric_keys_and_round_trip(Type type, int keyCount)
    {
        var attribute = type.GetCustomAttribute<MessagePackObjectAttribute>();
        attribute.Should().NotBeNull();
        attribute!.AllowPrivate.Should().BeTrue();
        type.GetProperties()
            .Select(property => property.GetCustomAttribute<KeyAttribute>())
            .OfType<KeyAttribute>()
            .Select(key => key.IntKey!.Value)
            .Order()
            .Should().Equal(Enumerable.Range(0, keyCount));
        var value = Activator.CreateInstance(type)!;
        typeof(OrderCompositionEventWireContractTests)
            .GetMethod(nameof(RoundTrip), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(type)
            .Invoke(null, [value]);
    }

    [Fact]
    public void Previous_close_event_array_remains_readable_with_no_invented_target_facts()
    {
        var source=new PortfolioCloseOrderCompositionCompletedEvent { Id=Guid.NewGuid(),SchemaVersion=1 };
        var bytes=MessagePackSerializer.Serialize(source,MessagePackBinarySerializer.ContentOptions);
        var reader=new MessagePackReader(bytes);reader.ReadArrayHeader().Should().Be(17);
        var start=(int)reader.Consumed;for(var i=0;i<16;i++) reader.Skip();var end=(int)reader.Consumed;
        var buffer=new System.Buffers.ArrayBufferWriter<byte>();var writer=new MessagePackWriter(buffer);
        writer.WriteArrayHeader(16);writer.WriteRaw(bytes.AsSpan(start,end-start));writer.Flush();
        var old=MessagePackBinarySerializer.Shared.Deserialize<PortfolioCloseOrderCompositionCompletedEvent>(buffer.WrittenSpan.ToArray());
        old.Id.Should().Be(source.Id);old.SchemaVersion.Should().Be(1);old.ProjectionFacts.Should().BeNull();
    }

    static void RoundTrip<T>(T value)
    {
        var bytes = MessagePackBinarySerializer.Shared.Serialize(value)!;
        var restored = MessagePackBinarySerializer.Shared.Deserialize<T>(bytes)!;
        var settings = new JsonSerializerSettings { DateTimeZoneHandling = DateTimeZoneHandling.Utc };
        JsonConvert.SerializeObject(restored, settings).Should()
            .Be(JsonConvert.SerializeObject(value, settings));
    }
}
