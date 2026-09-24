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
        { typeof(PortfolioCloseOrderCompositionCompletedEvent), 16 },
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

    static void RoundTrip<T>(T value)
    {
        var bytes = MessagePackBinarySerializer.Shared.Serialize(value)!;
        var restored = MessagePackBinarySerializer.Shared.Deserialize<T>(bytes)!;
        var settings = new JsonSerializerSettings { DateTimeZoneHandling = DateTimeZoneHandling.Utc };
        JsonConvert.SerializeObject(restored, settings).Should()
            .Be(JsonConvert.SerializeObject(value, settings));
    }
}
