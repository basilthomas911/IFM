using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.Architecture;

/// <summary>Guards the published direct-key Feed event layouts and watchdog default request.</summary>
public sealed class FeedMessageWireConventionTests
{
    public static TheoryData<Type> EventTypes => new()
    {
        typeof(FuturesTickTradeDataChangedEvent),
        typeof(FuturesTickQuoteDataChangedEvent),
        typeof(FuturesTickTradeDataInsertedEvent),
        typeof(FuturesTickQuoteDataInsertedEvent),
        typeof(FuturesMarketPriceUpdatedRealtimeEvent),
        typeof(FuturesSessionStatisticsUpdatedRealtimeEvent),
        typeof(FuturesEodSessionStatisticsUpdatedEvent),
    };

    [Theory, MemberData(nameof(EventTypes))]
    public void Event_numeric_keys_match_serialization_constructor_and_round_trip(Type messageType)
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
        var method = typeof(FeedMessageWireConventionTests).GetMethod(nameof(RoundTrip), BindingFlags.NonPublic | BindingFlags.Static)!;
        method.MakeGenericMethod(messageType).Invoke(null, null);
    }

    [Fact]
    public void Watchdog_history_default_constructor_preserves_request_identity()
    {
        var query = new GetDatabentoWatchdogHistoryQuery();
        query.EntityId.Should().NotBeNull();
        query.PageSize.Should().Be(100);
    }

    static void RoundTrip<T>() where T : new()
    {
        object source = new T();
        if (source is FuturesTickQuoteDataChangedEvent changed)
            source = changed with { QuoteData = new FuturesTickQuoteDataSegment([default], 1) };
        if (source is FuturesTickQuoteDataInsertedEvent inserted)
            source = inserted with { QuoteCount = 1, QuoteData = new FuturesTickQuoteDataSegment([default], 1) };
        var bytes = MessagePackSerializer.Serialize((T)source);
        var copy = MessagePackSerializer.Deserialize<T>(bytes);
        copy.Should().NotBeNull();
        MessagePackSerializer.Serialize(copy).Should().NotBeEmpty();
        if (copy is FuturesTickQuoteDataChangedEvent changedCopy) changedCopy.QuoteData.Dispose();
        if (copy is FuturesTickQuoteDataInsertedEvent insertedCopy) insertedCopy.QuoteData.Dispose();
    }
}
