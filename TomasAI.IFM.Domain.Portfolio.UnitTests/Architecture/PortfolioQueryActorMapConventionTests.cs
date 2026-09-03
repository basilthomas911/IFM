using System.Collections;
using System.Reflection;
using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Query.Actor;
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
        var expectedVerbs = typeof(PortfolioQueryVerbs)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

        parseMap.Keys.Cast<string>().Should().BeEquivalentTo(expectedVerbs);
        receiveMap.Count.Should().Be(parseMap.Count);
        exceptionMap.Keys.Cast<Type>().Should().BeEquivalentTo(receiveMap.Keys.Cast<Type>());
    }

    static IDictionary GetMap(string fieldName)
    {
        var field = typeof(PortfolioQueryActor).GetField(
            fieldName,
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        field.Should().NotBeNull($"PortfolioQueryActor must declare {fieldName}");
        return field!.GetValue(null).Should().BeAssignableTo<IDictionary>().Subject;
    }
}
