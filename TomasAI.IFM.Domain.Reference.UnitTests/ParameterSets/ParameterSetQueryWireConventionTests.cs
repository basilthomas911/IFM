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
        typeof(ListParameterComponentsQuery),
        typeof(ListParameterVersionsQuery),
        typeof(ValidateParameterCandidateQuery),
        typeof(CreateParameterDraftPreviewQuery),
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

    static void RoundTrip<T>() where T : new()
    {
        var bytes = MessagePackSerializer.Serialize(new T());
        var copy = MessagePackSerializer.Deserialize<T>(bytes);
        copy.Should().NotBeNull();
        MessagePackSerializer.Serialize(copy).Should().Equal(bytes);
    }
}
