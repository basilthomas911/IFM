using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;

/// <summary>Guards the permanent numeric wire layout of the ParameterSet query family.</summary>
public sealed class ParameterSetQueryWireConventionTests
{
    public static TheoryData<Type> QueryTypes => new()
    {
        typeof(PreviewTypedParameterDraftQuery),
        typeof(ValidateTypedParameterCandidateQuery),
        typeof(ListParameterComponentsQuery),
        typeof(ListParameterVersionsQuery),
        typeof(GetParameterSetStateQuery),
        typeof(GetParameterAssignmentQuery),
        typeof(GetParameterSchemaQuery),
        typeof(PreviewSignalStartupPlanQuery),
        typeof(GetParameterStartupRunQuery),
        typeof(GetParameterStartupRunsQuery),
        typeof(GetParameterStartupReportQuery),
        typeof(GetParameterSignalMonitoringQuery),
        typeof(ListLegacyParameterVersionsQuery),
        typeof(PreviewLegacyParameterMigrationQuery),
    };

    [Theory, MemberData(nameof(QueryTypes))]
    public void Published_query_keys_and_serialization_constructor_remain_consistent(Type queryType)
    {
        var properties = queryType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
            .Select(property => (Property: property, Key: property.GetCustomAttribute<KeyAttribute>()?.IntKey))
            .Where(item => item.Key.HasValue)
            .OrderBy(item => item.Key)
            .ToArray();
        properties.Select(item => item.Key!.Value).Should().Equal(Enumerable.Range(0, properties.Length));
        properties[0].Property.Name.Should().Be("Subject");
        properties[1].Property.Name.Should().Be("EntityId");
        var constructor = queryType.GetConstructors().Single(ctor => ctor.IsDefined(typeof(SerializationConstructorAttribute)));
        constructor.GetParameters().Select(parameter => parameter.ParameterType)
            .Should().Equal(properties.Select(item => item.Property.PropertyType));
        var roundTrip = typeof(ParameterSetQueryWireConventionTests).GetMethod(nameof(RoundTrip), BindingFlags.NonPublic | BindingFlags.Static)!;
        roundTrip.MakeGenericMethod(queryType).Invoke(null, null);
    }

    [Fact]
    public void Explicit_assignment_scope_round_trips_and_original_four_field_queries_still_read()
    {
        var scope = TomasAI.IFM.Domain.Reference.ParameterSets.Model.OptionSpreadStrategyParameterScopeModel.IronCondor();
        var query = new GetParameterAssignmentQuery { Scope = scope };
        MessagePackSerializer.Deserialize<GetParameterAssignmentQuery>(MessagePackSerializer.Serialize(query)).Scope.Should().Be(scope);
        var bytes = MessagePackSerializer.Serialize(new GetParameterAssignmentQuery());
        bytes[0].Should().Be(0x95); // Five-field array; the appended optional scope is nil.
        bytes[^1].Should().Be(0xc0);
        bytes[0] = 0x94;
        var old = MessagePackSerializer.Deserialize<GetParameterAssignmentQuery>(bytes.AsMemory(0, bytes.Length - 1));
        old.Scope.Should().BeNull();
        old.WorkflowDefinitionId.Should().BeEmpty();
    }

    static void RoundTrip<T>() where T : new()
    {
        var bytes = MessagePackSerializer.Serialize(new T());
        var copy = MessagePackSerializer.Deserialize<T>(bytes);
        copy.Should().NotBeNull();
        MessagePackSerializer.Serialize(copy).Should().Equal(bytes);
    }
}
