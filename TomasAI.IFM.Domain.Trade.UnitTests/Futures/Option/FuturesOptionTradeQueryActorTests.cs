using System.Reflection;
using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Futures.Option;

public sealed class FuturesOptionTradeQueryActorTests
{
    static readonly Type[] MigratedQueryTypes =
    [
        typeof(GetTradeHistoryQuery),
        typeof(GetTradeLimitQuery),
        typeof(GetTradePositionQuery),
        typeof(GetTradeQuantityQuery),
        typeof(GetTradeTypeLimitQuery)
    ];

    static readonly string[] MigratedQueryVerbs =
    [
        GetTradeHistoryQuery.Verb,
        GetTradeLimitQuery.Verb,
        GetTradePositionQuery.Verb,
        GetTradeQuantityQuery.Verb,
        GetTradeTypeLimitQuery.Verb
    ];

    [Fact]
    public void Parse_map_contains_every_migrated_option_query()
    {
        var parseMap = GetStaticMap<string, Func<IActorMessage, IQuery>>("_parseMap");

        parseMap.Keys.Should().Contain(MigratedQueryVerbs);
    }

    [Fact]
    public void Receive_map_contains_every_migrated_option_query()
    {
        var receiveMap = GetStaticMap<Type,
            Func<IFuturesOptionTradeQueryContext, IQuery, CancellationToken, ValueTask>>(
            "_receiveMap");

        receiveMap.Keys.Should().Contain(MigratedQueryTypes);
    }

    static IReadOnlyDictionary<TKey, TValue> GetStaticMap<TKey, TValue>(string fieldName)
        where TKey : notnull
    {
        var field = typeof(FuturesOptionTradeQueryActor).GetField(
            fieldName,
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Missing {fieldName} actor map.");
        return (IReadOnlyDictionary<TKey, TValue>)field.GetValue(null)!;
    }
}
