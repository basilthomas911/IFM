using System.Text;
using System.Security.Cryptography;
using System.Reflection;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using FluentAssertions;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Application.DatabaseBackup.Policies;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.UnitTests;

public sealed class SharedPolicyTests
{
    [Fact]
    public void Aws_used_actor_contract_messagepack_shape_matches_the_golden_fingerprint()
    {
        var contracts = typeof(DatabaseRecoveryOperationId).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && type.Namespace?.Contains(".DatabaseBackup.", StringComparison.Ordinal) == true)
            .Where(type => typeof(DatabaseBackupCommand).IsAssignableFrom(type)
                || typeof(DatabaseBackupInternalCommand).IsAssignableFrom(type)
                || typeof(DatabaseBackupEventContract).IsAssignableFrom(type)
                || typeof(DatabaseBackupQuery).IsAssignableFrom(type))
            .OrderBy(static type => type.FullName)
            .ToArray();
        contracts.Should().HaveCount(120);
        var shape = string.Join("\n", contracts.Select(type => type.FullName + ":" + string.Join(",",
            type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => (Property: property, Key: property.GetCustomAttributes(true)
                    .FirstOrDefault(attribute => attribute.GetType().FullName == "MessagePack.KeyAttribute")))
                .Where(static value => value.Key is not null)
                .Select(value => new
                {
                    value.Property.Name,
                    Type = value.Property.PropertyType.FullName,
                    Key = value.Key!.GetType().GetProperty("IntKey")?.GetValue(value.Key)
                })
                .OrderBy(static value => value.Key)
                .Select(static value => $"{value.Key}:{value.Name}:{value.Type}"))));
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(shape)));
        fingerprint.Should().Be("a91e64c69448802ae2e453c597798f30587cb252ddaac33acb7fa84fa9001d87");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Manifest_v1_and_v2_round_trip_canonically(int schemaVersion)
    {
        var manifest = Manifest(schemaVersion);
        var first = DatabaseBackupCanonicalJson.Serialize(manifest);
        var copy = DatabaseBackupCanonicalJson.Deserialize<DatabaseBackupManifest>(first);
        var second = DatabaseBackupCanonicalJson.Serialize(copy);
        second.Should().Equal(first);
        DatabaseBackupManifestPolicy.Validate(copy);
    }

    [Fact]
    public void Canonical_reader_rejects_duplicate_or_unknown_properties()
    {
        var duplicate = Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"schemaVersion\":2}");
        var unknown = Encoding.UTF8.GetBytes("{\"unknown\":1}");
        FluentActions.Invoking(() => DatabaseBackupCanonicalJson.Deserialize<DatabaseBackupManifest>(duplicate)).Should().Throw<Exception>();
        FluentActions.Invoking(() => DatabaseBackupCanonicalJson.Deserialize<DatabaseBackupManifest>(unknown)).Should().Throw<Exception>();
    }

    [Fact]
    public void Manifest_policy_rejects_cycles_duplicate_artifacts_and_non_utc_time()
    {
        var valid = Manifest(2);
        var self = valid with { Dependencies = [valid.RestorePointId] };
        var duplicates = valid with { Artifacts = [valid.Artifacts[0], valid.Artifacts[0]] };
        var localTime = valid with { CreatedUtc = new DateTimeOffset(2026, 8, 21, 1, 2, 3, TimeSpan.FromHours(-4)) };
        FluentActions.Invoking(() => DatabaseBackupManifestPolicy.Validate(self)).Should().Throw<InvalidDataException>();
        FluentActions.Invoking(() => DatabaseBackupManifestPolicy.Validate(duplicates)).Should().Throw<InvalidDataException>();
        FluentActions.Invoking(() => DatabaseBackupManifestPolicy.Validate(localTime)).Should().Throw<InvalidDataException>();
    }

    static DatabaseBackupManifest Manifest(int schemaVersion)
    {
        var operation = new DatabaseRecoveryOperationId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var restorePoint = new DatabaseRestorePointId(operation.Format());
        return new()
        {
            SchemaVersion = schemaVersion,
            ManifestId = "manifest-fixed",
            OperationId = operation,
            RestorePointId = restorePoint,
            Source = BackupSource.AwsCloud,
            Engine = DatabaseEngine.PostgreSql,
            ProtectionSetId = new("core-postgresql"),
            SafeBoundaryReference = "boundary-fixed",
            CreatedUtc = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero),
            Artifacts = [new("artifact.bin", 3, new string('a', 64))],
            Replicas = [new("primary-vault")],
            BackupLineage = schemaVersion == 1 ? new() : new()
            {
                RequestedMode = DatabaseBackupMode.Full,
                ResolvedMode = DatabaseBackupMode.Full,
                NativeKind = DatabaseNativeBackupKind.PostgreSqlBase,
                BaseRestorePointId = restorePoint,
                ChainDepth = 0
            }
        };
    }
}
