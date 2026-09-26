using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;

/// <summary>Guards the permanent numeric wire layout of the ParameterSet command family.</summary>
public sealed class ParameterSetCommandWireConventionTests
{
    public static TheoryData<Type> CommandTypes => new()
    {
        typeof(AssignParameterVersionCommand),
        typeof(DisableParameterAssignmentCommand),
        typeof(CreateParameterSetCommand),
        typeof(SaveParameterDraftCommand),
        typeof(RenameParameterSetCommand),
        typeof(PublishParameterVersionCommand),
        typeof(RetireParameterVersionCommand),
        typeof(ApplySignalStartupPlanCommand),
        typeof(ReleaseSignalStartupPlanCommand),
        typeof(RecordSignalStartupReportCommand),
    };

    [Theory, MemberData(nameof(CommandTypes))]
    public void Published_command_keys_and_serialization_constructor_remain_consistent(Type commandType)
    {
        var properties = commandType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
            .Select(property => (Property: property, Key: property.GetCustomAttribute<KeyAttribute>()?.IntKey))
            .Where(item => item.Key.HasValue)
            .OrderBy(item => item.Key)
            .ToArray();
        properties.Select(item => item.Key!.Value).Should().Equal(Enumerable.Range(0, properties.Length));
        properties.Take(6).Select(item => item.Property.Name)
            .Should().Equal("CommandId", "Subject", "PostEvents", "EntityId", "ErrorCode", "RouteTo");
        var constructor = commandType.GetConstructors().Single(ctor => ctor.IsDefined(typeof(SerializationConstructorAttribute)));
        constructor.GetParameters().Select(parameter => parameter.ParameterType)
            .Should().Equal(properties.Select(item => item.Property.PropertyType));
        var roundTrip = typeof(ParameterSetCommandWireConventionTests).GetMethod(nameof(RoundTrip), BindingFlags.NonPublic | BindingFlags.Static)!;
        roundTrip.MakeGenericMethod(commandType).Invoke(null, null);
    }

    static void RoundTrip<T>() where T : new()
    {
        var bytes = MessagePackSerializer.Serialize(new T());
        var copy = MessagePackSerializer.Deserialize<T>(bytes);
        copy.Should().NotBeNull();
        MessagePackSerializer.Serialize(copy).Should().Equal(bytes);
    }
}
