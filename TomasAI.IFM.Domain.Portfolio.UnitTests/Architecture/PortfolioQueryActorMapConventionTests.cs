using System.Collections;
using System.Reflection;
using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Query.Actor;
using TomasAI.IFM.Domain.Portfolio.Fund.Query.Actor;
using TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Query.Actor;
using TomasAI.IFM.Domain.Portfolio.Shared.Queries;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Architecture;

public sealed class PortfolioQueryActorMapConventionTests
{
    [Fact]
    public void Parse_receive_and_exception_maps_expose_the_same_complete_query_set()
    {
        var parseMap = GetMap("_parseMap");
        var receiveMap = GetMap("_receiveMap");
        var exceptionMap = GetMap("_exceptionMap");
        string[] expectedVerbs =
        [
            GetPortfolioQuery.Verb,
            GetPortfolioRevisionQuery.Verb,
            GetPortfoliosQuery.Verb,
            AllocatePortfolioBusinessIdQuery.Verb,
        ];

        parseMap.Keys.Cast<string>().Should().BeEquivalentTo(expectedVerbs);
        receiveMap.Count.Should().Be(parseMap.Count);
        exceptionMap.Keys.Cast<Type>().Should().BeEquivalentTo(receiveMap.Keys.Cast<Type>());
    }

    [Theory]
    [InlineData(typeof(PortfolioFundQueryActor), 14)]
    [InlineData(typeof(PortfolioFinancialPolicyQueryActor), 3)]
    public void Split_query_actors_have_parse_receive_and_exception_map_parity(Type actorType, int expectedMessages)
    {
        var parse = GetMap(actorType, "_parseMap");
        var receive = GetMap(actorType, "_receiveMap");
        var exceptions = GetMap(actorType, "_exceptionMap");

        parse.Count.Should().Be(expectedMessages);
        receive.Count.Should().Be(expectedMessages);
        exceptions.Keys.Cast<Type>().Should().BeEquivalentTo(receive.Keys.Cast<Type>());
    }

    static IDictionary GetMap(string fieldName)
        => GetMap(typeof(PortfolioQueryActor), fieldName);

    static IDictionary GetMap(Type actorType, string fieldName)
    {
        var field = actorType.GetField(
            fieldName,
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        field.Should().NotBeNull($"{actorType.Name} must declare {fieldName}");
        return field!.GetValue(null).Should().BeAssignableTo<IDictionary>().Subject;
    }
}
