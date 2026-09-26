using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;

namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests.DatabaseBackup;

/// <summary>Pins the canonical direct-key database-backup query schema.</summary>
public sealed class DatabaseBackupQueryWireTests
{
    public static TheoryData<Type> QueryTypes => new()
    {
        typeof(GetDatabaseProtectionSetsQuery),
        typeof(GetDatabaseBackupPolicyQuery),
        typeof(GetDatabaseBackupOperationQuery),
        typeof(ListDatabaseBackupOperationsQuery),
        typeof(GetDatabaseBackupSetQuery),
        typeof(ListDatabaseRestorePointsQuery),
        typeof(GetDatabaseRestorePointQuery),
        typeof(GetLatestVerifiedDatabaseBackupQuery),
        typeof(GetLatestRestoreTestedDatabaseBackupQuery),
        typeof(GetDatabaseRecoveryObjectiveComplianceQuery),
        typeof(GetDatabaseRestoreOperationQuery),
        typeof(ListDatabaseRestoreDrillsQuery),
        typeof(GetDatabaseRetentionForecastQuery),
        typeof(GetDatabaseBackupServiceHealthQuery),
        typeof(GetDatabaseRecoveryRunStatsQuery),
    };

    [Theory, MemberData(nameof(QueryTypes))]
    public void Every_query_has_direct_contiguous_keys_and_round_trips(Type type)
    {
        var keys = type.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
            .Select(property => (Property: property, Key: property.GetCustomAttribute<KeyAttribute>()?.IntKey))
            .Where(item => item.Key.HasValue)
            .OrderBy(item => item.Key)
            .ToArray();
        keys.Select(item => item.Key!.Value).Should().Equal(Enumerable.Range(0, 14));
        keys[0].Property.Name.Should().Be("Subject");
        keys[1].Property.Name.Should().Be("EntityId");
        var constructor = type.GetConstructors().Single(ctor => ctor.IsDefined(typeof(SerializationConstructorAttribute)));
        constructor.GetParameters().Select(parameter => parameter.ParameterType)
            .Should().Equal(keys.Select(item => item.Property.PropertyType));
        var roundTrip = typeof(DatabaseBackupQueryWireTests)
            .GetMethod(nameof(RoundTrip), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(type);
        roundTrip.Invoke(null, null);
    }

    static void RoundTrip<T>() where T : new()
    {
        var value = MessagePackSerializer.Deserialize<T>(MessagePackSerializer.Serialize(new T()));
        value.Should().NotBeNull();
    }
}
