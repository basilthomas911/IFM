using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Queries;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Architecture;

/// <summary>Guards the published IntrinsicTime workflow query wire layouts.</summary>
public sealed class IntrinsicTimeWorkflowQueryWireTests
{
    public static TheoryData<Type> QueryTypes => new()
    {
        typeof(GetIntrinsicTimeStrategyWorkflowByIdQuery),
        typeof(GetIntrinsicTimeStrategyWorkflowsByIdsQuery),
        typeof(GetActiveIntrinsicTimeStrategyWorkflowQuery),
        typeof(GetIntrinsicTimeStrategyWorkflowStartAttemptsQuery),
        typeof(GetIntrinsicTimeStrategyWorkflowStageStateQuery),
        typeof(GetIntrinsicTimeStrategyWorkflowTimelineQuery),
        typeof(GetRecentIntrinsicTimeStrategyWorkflowsQuery),
        typeof(GetCompletedIntrinsicTimeStrategyWorkflowsQuery),
        typeof(GetStoppedIntrinsicTimeStrategyWorkflowsQuery),
        typeof(GetIntrinsicTimeStrategyWorkflowObservationQuery),
        typeof(GetIntrinsicTimeStrategyWorkflowHistoryPageQuery),
    };

    [Theory, MemberData(nameof(QueryTypes))]
    public void Query_keys_match_constructor_and_round_trip(Type queryType)
    {
        var keys = queryType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
            .Select(property => (Property: property, Key: property.GetCustomAttribute<KeyAttribute>()?.IntKey))
            .Where(item => item.Key.HasValue)
            .OrderBy(item => item.Key)
            .ToArray();
        keys.Select(item => item.Key!.Value).Should().Equal(Enumerable.Range(0, keys.Length));
        keys.Take(2).Select(item => item.Property.Name).Should().Equal("Subject", "EntityId");
        var constructor = queryType.GetConstructors().Single(ctor => ctor.IsDefined(typeof(SerializationConstructorAttribute)));
        constructor.GetParameters().Select(parameter => parameter.ParameterType)
            .Should().Equal(keys.Select(item => item.Property.PropertyType));
        var method = typeof(IntrinsicTimeWorkflowQueryWireTests).GetMethod(nameof(RoundTrip), BindingFlags.NonPublic | BindingFlags.Static)!;
        method.MakeGenericMethod(queryType).Invoke(null, null);
    }

    static void RoundTrip<T>() where T : new()
    {
        var bytes = MessagePackSerializer.Serialize(new T());
        var copy = MessagePackSerializer.Deserialize<T>(bytes);
        copy.Should().NotBeNull();
    }
}
