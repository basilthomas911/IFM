using FluentAssertions;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Signing;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.UnitTests;

public sealed class AwsRecoverySourceQualificationPolicyTests
{
    [Fact]
    [Trait("Category", "Gate13")]
    public void Equivalent_independently_encrypted_recovery_replica_is_qualified()
    {
        var options = Options();
        var primary = Point("aws-primary", options.PrimaryBucketName, options.PrimaryRegion, options.PrimaryEncryptionKeyArn);
        var recovery = Point("aws-recovery", options.RecoveryBucketName, options.RecoveryRegion, options.RecoveryEncryptionKeyArn);

        var action = () => AwsRecoverySourceQualificationPolicy.Validate(primary, recovery, options);

        action.Should().NotThrow();
    }

    [Theory]
    [InlineData("bucket")]
    [InlineData("region")]
    [InlineData("key")]
    [InlineData("checksum")]
    [InlineData("retention")]
    [Trait("Category", "Gate13")]
    public void Recovery_replica_metadata_or_content_drift_fails_closed(string drift)
    {
        var options = Options();
        var primary = Point("aws-primary", options.PrimaryBucketName, options.PrimaryRegion, options.PrimaryEncryptionKeyArn);
        var recovery = Point("aws-recovery", options.RecoveryBucketName, options.RecoveryRegion, options.RecoveryEncryptionKeyArn);
        var artifact = recovery.Publication.Artifacts.Single();
        var changed = artifact.Object with
        {
            BucketName = drift == "bucket" ? options.PrimaryBucketName : artifact.Object.BucketName,
            Region = drift == "region" ? options.PrimaryRegion : artifact.Object.Region,
            EncryptionKeyArn = drift == "key" ? options.PrimaryEncryptionKeyArn : artifact.Object.EncryptionKeyArn,
            Sha256 = drift == "checksum" ? new string('B', 64) : artifact.Object.Sha256,
            RetainUntilUtc = drift == "retention" ? artifact.Object.PublishedUtc : artifact.Object.RetainUntilUtc
        };
        recovery = recovery with
        {
            Publication = recovery.Publication with
            {
                Artifacts = [artifact with { Object = changed }]
            }
        };

        var action = () => AwsRecoverySourceQualificationPolicy.Validate(primary, recovery, options);

        action.Should().Throw<InvalidDataException>();
    }

    static AwsResolvedCatalogRestorePoint Point(string replica, string bucket, string region, string keyArn)
    {
        var restorePointId = new DatabaseRestorePointId("restore-1");
        var replicaId = new DatabaseArtifactReplicaId(replica);
        var protectionSet = new DatabaseProtectionSetId("scylla-core");
        var now = new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);
        var manifest = new DatabaseBackupManifest
        {
            ManifestId = "manifest-1",
            OperationId = new DatabaseRecoveryOperationId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            RestorePointId = restorePointId,
            Engine = DatabaseEngine.ScyllaDb,
            ProtectionSetId = protectionSet,
            SafeBoundaryReference = "scylla-snapshot-1234567890123456",
            CreatedUtc = now,
            Dependencies = [],
            Artifacts = [new DatabaseArtifactDigest("v1/artifact", 16, new string('A', 64))],
            Replicas = [replicaId],
            BackupLineage = new DatabaseBackupLineage
            {
                RequestedMode = DatabaseBackupMode.Full,
                ResolvedMode = DatabaseBackupMode.Full,
                NativeKind = DatabaseNativeBackupKind.ScyllaManagerSnapshot,
                NativeIdentity = "cluster-a:keyspace-a"
            }
        };
        var artifact = Object(bucket, region, keyArn, "v1/artifact", now);
        var publicationObject = Object(bucket, region, keyArn, "v1/publication", now);
        var manifestObject = Object(bucket, region, keyArn, "v1/manifest", now);
        var record = new AwsPublicationRecord
        {
            OperationId = manifest.OperationId,
            RestorePointId = restorePointId,
            ReplicaId = replicaId,
            ProtectionSetId = protectionSet,
            Engine = DatabaseEngine.ScyllaDb,
            Artifacts = [new AwsPublishedArtifact("native/data.db", artifact)],
            EngineManifest = manifestObject,
            EngineManifestSha256 = new string('A', 64),
            EngineManifestSignature = new AwsSignatureEnvelope
            {
                KeyArn = "arn:aws:kms:ca-central-1:107651266250:key/signing",
                Algorithm = "ECDSA_SHA_256",
                DigestAlgorithm = "SHA-256",
                DigestBase64 = "digest",
                SignatureBase64 = "signature",
                SignedUtc = now
            },
            Dependencies = [],
            ScyllaTopology = new ScyllaTopologyEvidence("cluster-a", 3, 768, true),
            ScyllaSnapshot = new ScyllaSnapshotEvidence(
                "snapshot-1", "backup/task-1", new string('A', 64), new string('B', 64), 1, 1, 1, "2025.1", "3.11"),
            ProducingHostId = "host-1",
            BuildIdentity = "test",
            PublishedUtc = now,
            VerifiedUtc = now
        };
        var entry = new AwsCatalogEntry
        {
            RestorePointId = restorePointId,
            ReplicaId = replicaId,
            ProtectionSetId = protectionSet,
            Engine = DatabaseEngine.ScyllaDb,
            PublicationRecord = publicationObject,
            PublicationRecordSha256 = new string('C', 64),
            PublishedUtc = now
        };
        return new AwsResolvedCatalogRestorePoint(
            entry,
            record,
            new DatabaseCatalogRestorePoint(
                new DatabaseCatalogEntry(1, restorePointId, manifest.ManifestId, 1, DatabaseEngine.ScyllaDb,
                    protectionSet, replicaId, "v1/manifest", "v1/publication", now),
                manifest,
                16,
                1));
    }

    static AwsImmutableObjectVersion Object(
        string bucket, string region, string keyArn, string objectKey, DateTimeOffset publishedUtc) => new()
    {
        BucketName = bucket,
        Region = region,
        ObjectKey = objectKey,
        VersionId = "version-1",
        Length = 16,
        Sha256 = new string('A', 64),
        S3ChecksumSha256 = "checksum",
        EncryptionKeyArn = keyArn,
        EncryptionContextBase64 = "e30=",
        ObjectLockMode = "Governance",
        RetainUntilUtc = publishedUtc.AddDays(35),
        PublishedUtc = publishedUtc
    };

    static AwsCloudDatabaseBackupOptions Options() => new()
    {
        PrimaryBucketName = "ifm-primary-development",
        RecoveryBucketName = "ifm-recovery-development",
        PrimaryRegion = "ca-central-1",
        RecoveryRegion = "ca-west-1",
        PrimaryEncryptionKeyArn = "arn:aws:kms:ca-central-1:107651266250:key/primary",
        RecoveryEncryptionKeyArn = "arn:aws:kms:ca-west-1:107651266250:key/recovery"
    };
}
