using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.Architecture;

/// <summary>Guards the published wire keys of the Analytics event and historical-loader family.</summary>
public sealed class AnalyticsEventWireConventionTests
{
    public static TheoryData<Type> MessageTypes => new()
    {
        typeof(LoadFuturesAnalyticsHistoricalDataCommand),
        typeof(FuturesAnalyticsHistoricalDataLoaderRequestedEvent),
        typeof(FuturesAnalyticsHistoricalDataLoaderCompletedEvent),
        typeof(FuturesAnalyticsHistoricalDataLoaderFailedEvent),
        typeof(GetFuturesAnalyticsHistoricalDataLoaderQuery),
        typeof(PublishFuturesTradeSessionBarCommand),
        typeof(FuturesBbSignalGeneratedCompleteEvent),
        typeof(FuturesEmaSignalGeneratedCompleteEvent),
        typeof(MarketOutlookComponentChangedRealtimeEvent),
        typeof(MarketOutlookEodUpdatedRealtimeEvent),
        typeof(MarketOutlookSnapshotInsertedEvent),
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
        var method = typeof(AnalyticsEventWireConventionTests).GetMethod(nameof(RoundTrip), BindingFlags.NonPublic | BindingFlags.Static)!;
        method.MakeGenericMethod(messageType).Invoke(null, null);
    }

    static void RoundTrip<T>() where T : new()
    {
        var bytes = MessagePackSerializer.Serialize(new T());
        var copy = MessagePackSerializer.Deserialize<T>(bytes);
        copy.Should().NotBeNull();
        var reencoded = MessagePackSerializer.Serialize(copy);
        MessagePackSerializer.Deserialize<T>(reencoded).Should().NotBeNull();
    }
}
