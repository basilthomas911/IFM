using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;

namespace TomasAI.IFM.Domain.MarketData.UnitTests.Architecture;

/// <summary>Guards published MarketData root actor-message layouts.</summary>
public sealed class MarketDataRootMessageWireConventionTests
{
    public static TheoryData<Type> MessageTypes => new()
    {
        typeof(GetDatabentoOptionChainQuery),
        typeof(GetDatabentoOptionChainRangeQuery),
        typeof(GetEvaluatedOptionChainQuery),
        typeof(GetInstrumentDefinitionsQuery),
        typeof(GetTradeStrategySymbolsQuery),
        typeof(GetMarketDataDownloadHistoryQuery),
        typeof(GetMarketDataDownloadLogQuery),
        typeof(GetMarketDataDownloadStatusQuery),
        typeof(InsertMarketDataDownloadLogCommand),
    };

    [Theory, MemberData(nameof(MessageTypes))]
    public void Published_numeric_keys_match_serialization_constructor_and_round_trip(Type messageType)
    {
        var keys = messageType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
            .Select(property => (Property: property, Key: property.GetCustomAttribute<KeyAttribute>()?.IntKey))
            .Where(item => item.Key.HasValue)
            .OrderBy(item => item.Key)
            .ToArray();
        keys.Select(item => item.Key!.Value).Should().Equal(Enumerable.Range(0, keys.Length));
        var constructor = messageType.GetConstructors().Single(ctor => ctor.IsDefined(typeof(SerializationConstructorAttribute)));
        constructor.GetParameters().Select(parameter => parameter.ParameterType)
            .Should().Equal(keys.Select(item => item.Property.PropertyType));
        var method = typeof(MarketDataRootMessageWireConventionTests).GetMethod(nameof(RoundTrip), BindingFlags.NonPublic | BindingFlags.Static)!;
        method.MakeGenericMethod(messageType).Invoke(null, null);
    }

    static void RoundTrip<T>() where T : new()
    {
        var bytes = MessagePackSerializer.Serialize(new T());
        var copy = MessagePackSerializer.Deserialize<T>(bytes);
        copy.Should().NotBeNull();
        MessagePackSerializer.Serialize(copy).Should().Equal(bytes);
    }
}
