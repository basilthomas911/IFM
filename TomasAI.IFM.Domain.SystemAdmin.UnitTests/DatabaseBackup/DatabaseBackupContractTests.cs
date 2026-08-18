using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests.DatabaseBackup;

public sealed class DatabaseBackupContractTests
{
    static readonly Guid OperationValue = Guid.Parse("ea01c609-b967-4db5-97fd-d4d36ed8a5ee");
    static readonly DatabaseRecoveryOperationId OperationId = new(OperationValue);
    static readonly DatabaseRequestEnvelope Request = new()
    {
        RequestId = OperationValue,
        CallerIdentity = "operator-1",
        AuthorizationReference = "approval-1",
        CallerRoles = ["DatabaseRecoveryOperator"],
        Origin = DatabaseRequestOrigin.Console,
        CorrelationId = Guid.Parse("ba45ce4b-94db-4c2e-b26a-4429fae02512"),
        EnvironmentIdentity = "paper-trading",
        CreatedUtc = new DateTimeOffset(2026, 8, 12, 12, 0, 0, TimeSpan.Zero)
    };
    static readonly DatabaseSourceEnvelope Source = new()
    {
        SourceEventId = Guid.Parse("6e729f55-04d2-4664-900c-c59051bc3177"),
        OperationId = OperationId,
        BackupSetId = new DatabaseBackupSetId(Guid.Parse("eedf1411-ec0e-4382-a31b-5ced99fb956b")),
        Source = BackupSource.LocalWorkstation,
        ProtectionSetId = new DatabaseProtectionSetId("core-databases"),
        PolicyRevision = 3,
        OperationKind = DatabaseRecoveryOperationKind.Backup,
        Phase = DatabaseRecoveryPhase.Requested,
        ProducingHostId = new DatabaseBackupHostId("backup-host-1"),
        SourceRevisionOrSequence = 7,
        CorrelationId = Request.CorrelationId,
        CausationId = Request.RequestId,
        ObservedUtc = new DateTimeOffset(2026, 8, 12, 12, 1, 0, TimeSpan.Zero)
    };

    [Fact]
    public void Strongly_typed_ids_round_trip_and_reject_path_like_values()
    {
        RoundTrip(OperationId).Should().Be(OperationId);
        RoundTrip(new DatabaseBackupSetId(Guid.NewGuid())).Value.Should().NotBeEmpty();
        RoundTrip(new DatabaseRetentionPlanId(Guid.NewGuid())).Value.Should().NotBeEmpty();
        RoundTrip(new DatabaseProtectionSetId("core")).Value.Should().Be("core");
        RoundTrip(new DatabaseRestorePointId("restore-001")).Value.Should().Be("restore-001");
        RoundTrip(new DatabaseBackupPolicyId("paper-policy")).Value.Should().Be("paper-policy");
        RoundTrip(new DatabaseBackupHostId("host-01")).Value.Should().Be("host-01");
        RoundTrip(new DatabaseArtifactId("artifact-01")).Value.Should().Be("artifact-01");
        RoundTrip(new DatabaseArtifactReplicaId("replica-01")).Value.Should().Be("replica-01");

        var act = () => new DatabaseArtifactId("../escape");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Enum_values_are_stable_and_unknown_sources_are_rejected()
    {
        ((int)BackupSource.LocalWorkstation).Should().Be(1);
        ((int)BackupSource.AwsCloud).Should().Be(2);
        ((int)DatabaseRecoveryOperationKind.Retention).Should().Be(7);
        ((int)DatabaseArtifactReplicaState.Deleted).Should().Be(8);
        ((int)DatabaseBackupMode.Automatic).Should().Be(1);
        ((int)DatabaseBackupMode.Full).Should().Be(2);
        ((int)DatabaseBackupMode.Incremental).Should().Be(3);

        var none = () => DatabaseBackupEnumValidation.RequireConcrete(BackupSource.None);
        var unknown = () => DatabaseBackupEnumValidation.RequireConcrete((BackupSource)99);
        none.Should().Throw<ArgumentOutOfRangeException>();
        unknown.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Incremental_lineage_round_trips_without_losing_restore_chain_identity()
    {
        var lineage = new DatabaseBackupLineage
        {
            RequestedMode = DatabaseBackupMode.Automatic,
            ResolvedMode = DatabaseBackupMode.Incremental,
            NativeKind = DatabaseNativeBackupKind.PostgreSqlIncremental,
            BaseRestorePointId = new DatabaseRestorePointId("base-001"),
            ParentRestorePointId = new DatabaseRestorePointId("incremental-003"),
            ChainDepth = 4,
            NativeIdentity = "postgres-system-42"
        };

        lineage.Validate(resolvedRequired: true);
        RoundTrip(lineage).Should().BeEquivalentTo(lineage);
    }

    [Fact]
    public void Envelopes_require_versioned_bounded_utc_metadata()
    {
        Request.Validate();
        Source.Validate();

        var localTime = Request with { CreatedUtc = new DateTimeOffset(2026, 8, 12, 8, 0, 0, TimeSpan.FromHours(-4)) };
        var tooManyRoles = Request with { CallerRoles = Enumerable.Repeat("role", DatabaseBackupContractLimits.MaximumCollectionCount + 1).ToArray() };
        var unsupportedVersion = Source with { ContractVersion = 2 };

        ((Action)localTime.Validate).Should().Throw<ArgumentException>();
        ((Action)tooManyRoles.Validate).Should().Throw<ArgumentOutOfRangeException>();
        ((Action)unsupportedVersion.Validate).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Every_concrete_actor_contract_has_a_working_messagepack_formatter()
    {
        var assembly = typeof(DatabaseRecoveryOperationId).Assembly;
        var actorContracts = assembly.GetTypes()
            .Where(type => !type.IsAbstract && type.Namespace?.Contains(".DatabaseBackup.", StringComparison.Ordinal) == true)
            .Where(type => typeof(DatabaseBackupCommand).IsAssignableFrom(type)
                || typeof(DatabaseBackupInternalCommand).IsAssignableFrom(type)
                || typeof(DatabaseBackupEventContract).IsAssignableFrom(type)
                || typeof(DatabaseBackupQuery).IsAssignableFrom(type))
            .OrderBy(type => type.FullName)
            .ToArray();

        actorContracts.Should().HaveCount(120);
        foreach (var type in actorContracts)
        {
            var instance = Activator.CreateInstance(type)!;
            PopulateCommonContract(instance);
            var bytes = MessagePackSerializer.Serialize(type, instance);
            var copy = MessagePackSerializer.Deserialize(type, bytes);
            copy.Should().NotBeNull(type.FullName);
        }
    }

    [Fact]
    public void Public_contract_shapes_do_not_expose_native_or_secret_fields()
    {
        string[] forbiddenTokens = ["ConnectionString", "Credential", "Password", "NativeArgument", "Executable", "Sql", "Cql", "BucketName", "FileSystemPath"];
        var exposed = typeof(DatabaseRecoveryOperationId).Assembly.GetTypes()
            .Where(type => type.Namespace?.Contains(".DatabaseBackup.", StringComparison.Ordinal) == true)
            .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => $"{type.Name}.{property.Name}"))
            .Where(shape => forbiddenTokens.Any(shape.Contains))
            .ToArray();

        exposed.Should().BeEmpty();
    }

    static void PopulateCommonContract(object instance)
    {
        Set(instance, nameof(DatabaseBackupCommand.CommandId), Request.RequestId);
        Set(instance, nameof(DatabaseBackupCommand.EntityId), OperationId);
        Set(instance, nameof(DatabaseBackupCommand.Request), Request);
        Set(instance, nameof(DatabaseBackupInternalCommand.Source), Source);
        Set(instance, nameof(DatabaseBackupEventContract.Id), Source.SourceEventId);
        Set(instance, nameof(DatabaseBackupEventContract.Source), Source);
        Set(instance, nameof(DatabaseBackupQuery.Request), Request);
        Set(instance, nameof(DatabaseBackupQuery.EntityId), OperationId);
        Set(instance, "Subject", new ActorSubject(ActorType.Command, "DatabaseBackup", "ContractTest", OperationId.Format()));
    }

    static void Set(object target, string propertyName, object value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property?.CanWrite == true && property.PropertyType.IsInstanceOfType(value)) property.SetValue(target, value);
    }

    static T RoundTrip<T>(T value)
        => MessagePackSerializer.Deserialize<T>(MessagePackSerializer.Serialize(value));
}
