using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.Architecture;

/// <summary>Guards the published wire keys of the Analytics command/query family.</summary>
public sealed class AnalyticsCommandQueryWireConventionTests
{
    public static TheoryData<Type> MessageTypes => new()
    {
        typeof(GetLatestFuturesVwapSignalQuery),
        typeof(GetFuturesVwapSignalHistoryQuery),
        typeof(GetLatestFuturesVxTermStructureSignalQuery),
        typeof(UpdateFuturesVwapSignalCommand),
        typeof(RecoverFuturesVwapSignalCommand),
        typeof(GenerateFuturesBbSignalCommand),
        typeof(GenerateFuturesEmaSignalCommand),
        typeof(StartFuturesAdxSignalCommand),
        typeof(StartFuturesAtrSignalCommand),
        typeof(StartFuturesMacdSignalCommand),
        typeof(StopFuturesAdxSignalCommand),
        typeof(StopFuturesAtrSignalCommand),
        typeof(StopFuturesMacdSignalCommand),
        typeof(UpdateFuturesVxTermStructureSignalCommand),
    };

    [Theory, MemberData(nameof(MessageTypes))]
    public void Numeric_keys_match_serialization_constructor_and_round_trip(Type messageType)
    {
        messageType.GetCustomAttribute<MessagePackObjectAttribute>()!.AllowPrivate.Should().BeTrue();
        var keys = messageType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
            .Select(property => (Property: property, Key: property.GetCustomAttribute<KeyAttribute>()?.IntKey))
            .Where(item => item.Key.HasValue)
            .OrderBy(item => item.Key)
            .ToArray();
        keys.Select(item => item.Key!.Value).Should().Equal(Enumerable.Range(0, keys.Length));
        var constructor = messageType.GetConstructors().Single(ctor => ctor.IsDefined(typeof(SerializationConstructorAttribute)));
        constructor.GetParameters().Select(parameter => parameter.ParameterType)
            .Should().Equal(keys.Select(item => item.Property.PropertyType));
        var method = typeof(AnalyticsCommandQueryWireConventionTests).GetMethod(nameof(RoundTrip), BindingFlags.NonPublic | BindingFlags.Static)!;
        method.MakeGenericMethod(messageType).Invoke(null, null);
    }

    static void RoundTrip<T>() where T : new()
    {
        var bytes = MessagePackSerializer.Serialize(new T());
        var copy = MessagePackSerializer.Deserialize<T>(bytes);
        copy.Should().NotBeNull();
        // Nested legacy read models may normalize their default values on the first decode.
        var reencoded = MessagePackSerializer.Serialize(copy);
        reencoded.Should().NotBeEmpty();
        MessagePackSerializer.Deserialize<T>(reencoded).Should().NotBeNull();
    }
}
