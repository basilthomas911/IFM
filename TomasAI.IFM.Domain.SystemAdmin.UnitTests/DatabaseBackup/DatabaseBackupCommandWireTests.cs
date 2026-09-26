using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;

namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests.DatabaseBackup;

/// <summary>Pins the canonical direct-key database-backup command schema.</summary>
public sealed class DatabaseBackupCommandWireTests
{
    public static TheoryData<Type> CommandTypes => new()
    {
        typeof(RequestDatabaseBackupCommand),
        typeof(CancelDatabaseBackupCommand),
        typeof(RequestDatabaseRestoreCommand),
        typeof(ApproveDatabaseRestoreCommand),
        typeof(CancelDatabaseRestoreCommand),
        typeof(ApproveDatabaseCutoverCommand),
        typeof(RequestDatabaseRestoreDrillCommand),
        typeof(UpdateDatabaseBackupPolicyCommand),
        typeof(PlaceBackupLegalHoldCommand),
        typeof(ReleaseBackupLegalHoldCommand),
        typeof(RequestBackupRetentionEvaluationCommand),
        typeof(ExecuteBackupRetentionPlanCommand),
    };

    [Theory, MemberData(nameof(CommandTypes))]
    public void Every_command_has_direct_standard_envelope_and_round_trips(Type type)
    {
        var keys = type.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
            .Select(property => (Property: property, Key: property.GetCustomAttribute<KeyAttribute>()?.IntKey))
            .Where(item => item.Key.HasValue)
            .OrderBy(item => item.Key)
            .ToArray();
        keys.Select(item => item.Key!.Value).Should().Equal(Enumerable.Range(0, 32));
        keys.Take(6).Select(item => item.Property.Name)
            .Should().Equal("CommandId", "Subject", "PostEvents", "EntityId", "ErrorCode", "RouteTo");
        var constructor = type.GetConstructors().Single(ctor => ctor.IsDefined(typeof(SerializationConstructorAttribute)));
        constructor.GetParameters().Select(parameter => parameter.ParameterType)
            .Should().Equal(keys.Select(item => item.Property.PropertyType));
        typeof(DatabaseBackupCommandWireTests)
            .GetMethod(nameof(RoundTrip), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(type).Invoke(null, null);
    }

    static void RoundTrip<T>() where T : new()
        => MessagePackSerializer.Deserialize<T>(MessagePackSerializer.Serialize(new T())).Should().NotBeNull();
}
