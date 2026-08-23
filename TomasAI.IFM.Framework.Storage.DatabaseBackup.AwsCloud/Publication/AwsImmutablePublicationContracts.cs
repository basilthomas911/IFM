using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Signing;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;

public sealed record AwsVaultLocation(
    string BucketName,
    string Region,
    string EncryptionKeyArn,
    DatabaseArtifactReplicaId ReplicaId);

public sealed record AwsImmutableObjectVersion
{
    public int SchemaVersion { get; init; } = 1;
    public required string BucketName { get; init; }
    public required string Region { get; init; }
    public required string ObjectKey { get; init; }
    public required string VersionId { get; init; }
    public required long Length { get; init; }
    public required string Sha256 { get; init; }
    public required string S3ChecksumSha256 { get; init; }
    public required string EncryptionKeyArn { get; init; }
    public required string EncryptionContextBase64 { get; init; }
    public required string ObjectLockMode { get; init; }
    public required DateTimeOffset RetainUntilUtc { get; init; }
    public required DateTimeOffset PublishedUtc { get; init; }
}

public sealed record AwsPublishedArtifact(
    string LogicalRelativePath,
    AwsImmutableObjectVersion Object);

public sealed record AwsPublicationRecord
{
    public int SchemaVersion { get; init; } = 1;
    public required DatabaseRecoveryOperationId OperationId { get; init; }
    public required DatabaseRestorePointId RestorePointId { get; init; }
    public required DatabaseArtifactReplicaId ReplicaId { get; init; }
    public required DatabaseProtectionSetId ProtectionSetId { get; init; }
    public required DatabaseEngine Engine { get; init; }
    public BackupSource Source { get; init; } = BackupSource.AwsCloud;
    public required AwsPublishedArtifact[] Artifacts { get; init; }
    public required AwsImmutableObjectVersion EngineManifest { get; init; }
    public required string EngineManifestSha256 { get; init; }
    public required AwsSignatureEnvelope EngineManifestSignature { get; init; }
    public DatabaseRestorePointId[] Dependencies { get; init; } = [];
    public string? PostgreSqlTimeline { get; init; }
    public string? PostgreSqlStartLsn { get; init; }
    public string? PostgreSqlEndLsn { get; init; }
    public ScyllaTopologyEvidence? ScyllaTopology { get; init; }
    public ScyllaSnapshotEvidence? ScyllaSnapshot { get; init; }
    public required string ProducingHostId { get; init; }
    public required string BuildIdentity { get; init; }
    public required DateTimeOffset PublishedUtc { get; init; }
    public required DateTimeOffset VerifiedUtc { get; init; }
}

public sealed record AwsSignedPublicationRecord(
    AwsPublicationRecord Record,
    AwsSignatureEnvelope Signature);

public sealed record AwsCatalogEntry
{
    public int SchemaVersion { get; init; } = 1;
    public required DatabaseRestorePointId RestorePointId { get; init; }
    public required DatabaseArtifactReplicaId ReplicaId { get; init; }
    public required DatabaseProtectionSetId ProtectionSetId { get; init; }
    public required DatabaseEngine Engine { get; init; }
    public required AwsImmutableObjectVersion PublicationRecord { get; init; }
    public required string PublicationRecordSha256 { get; init; }
    public required DateTimeOffset PublishedUtc { get; init; }
}

public sealed record AwsResolvedCatalogRestorePoint(
    AwsCatalogEntry Entry,
    AwsPublicationRecord Publication,
    DatabaseCatalogRestorePoint RestorePoint,
    AwsImmutableObjectVersion[]? ImmutableObjects = null);

public readonly record struct AwsGeneratedObjectKey
{
    public AwsGeneratedObjectKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 1024 || value.StartsWith('/')
            || value.Contains("//", StringComparison.Ordinal) || value.Contains("..", StringComparison.Ordinal)
            || value.Any(char.IsControl))
            throw new ArgumentException("The generated AWS backup object key is unsafe.", nameof(value));
        Value = value;
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public sealed class AwsBackupObjectKeyFactory(string environment)
{
    readonly string _environment = Segment(environment, nameof(environment));

    public AwsGeneratedObjectKey Artifact(
        DatabaseProtectionSetId protectionSetId, DatabaseEngine engine,
        DatabaseRestorePointId restorePointId, DatabaseArtifactId artifactId, string fileName)
        => Key(protectionSetId, engine, restorePointId,
            $"artifacts/{Segment(artifactId.Value, nameof(artifactId))}/{Segment(fileName, nameof(fileName))}");

    public AwsGeneratedObjectKey EngineManifest(
        DatabaseProtectionSetId protectionSetId, DatabaseEngine engine, DatabaseRestorePointId restorePointId)
        => Key(protectionSetId, engine, restorePointId, "manifests/engine-manifest-v2.json");

    public AwsGeneratedObjectKey EngineManifestSignature(
        DatabaseProtectionSetId protectionSetId, DatabaseEngine engine, DatabaseRestorePointId restorePointId)
        => Key(protectionSetId, engine, restorePointId, "manifests/engine-manifest-v2.signature.json");

    public AwsGeneratedObjectKey Publication(
        DatabaseProtectionSetId protectionSetId, DatabaseEngine engine, DatabaseRestorePointId restorePointId,
        DatabaseArtifactReplicaId replicaId)
        => Key(protectionSetId, engine, restorePointId,
            $"publications/{Segment(replicaId.Value, nameof(replicaId))}/publication-v1.json");

    public AwsGeneratedObjectKey PublicationSignature(
        DatabaseProtectionSetId protectionSetId, DatabaseEngine engine, DatabaseRestorePointId restorePointId,
        DatabaseArtifactReplicaId replicaId)
        => Key(protectionSetId, engine, restorePointId,
            $"publications/{Segment(replicaId.Value, nameof(replicaId))}/publication-v1.signature.json");

    public AwsGeneratedObjectKey Catalog(DatabaseRestorePointId restorePointId, DatabaseArtifactReplicaId replicaId)
        => new($"v1/environment/{_environment}/catalog/restore-point/{Segment(restorePointId.Value, nameof(restorePointId))}/{Segment(replicaId.Value, nameof(replicaId))}/catalog-entry-v1.json");

    public AwsGeneratedObjectKey Wal(DatabaseProtectionSetId protectionSetId, string timeline, string segmentName)
        => new($"v1/environment/{_environment}/protection-set/{Segment(protectionSetId.Value, nameof(protectionSetId))}/postgresql/timeline/{Segment(timeline, nameof(timeline))}/wal/{Segment(segmentName, nameof(segmentName))}");

    public AwsGeneratedObjectKey WalRecord(DatabaseProtectionSetId protectionSetId, string timeline, string segmentName)
        => new($"v1/environment/{_environment}/protection-set/{Segment(protectionSetId.Value, nameof(protectionSetId))}/postgresql/timeline/{Segment(timeline, nameof(timeline))}/wal-index/{Segment(segmentName, nameof(segmentName))}/record-v1.json");

    public AwsGeneratedObjectKey WalRecordSignature(DatabaseProtectionSetId protectionSetId, string timeline, string segmentName)
        => new($"v1/environment/{_environment}/protection-set/{Segment(protectionSetId.Value, nameof(protectionSetId))}/postgresql/timeline/{Segment(timeline, nameof(timeline))}/wal-index/{Segment(segmentName, nameof(segmentName))}/record-v1.signature.json");

    public AwsGeneratedObjectKey Evidence(DatabaseRecoveryOperationId operationId, string documentName)
        => new($"v1/environment/{_environment}/evidence/operation/{operationId.Format()}/{Segment(documentName, nameof(documentName))}.json");

    public AwsGeneratedObjectKey EvidenceSignature(DatabaseRecoveryOperationId operationId, string documentName)
        => new($"v1/environment/{_environment}/evidence/operation/{operationId.Format()}/{Segment(documentName, nameof(documentName))}.signature.json");

    AwsGeneratedObjectKey Key(DatabaseProtectionSetId protectionSetId, DatabaseEngine engine,
        DatabaseRestorePointId restorePointId, string suffix)
        => new($"v1/environment/{_environment}/protection-set/{Segment(protectionSetId.Value, nameof(protectionSetId))}/engine/{Engine(engine)}/restore-point/{Segment(restorePointId.Value, nameof(restorePointId))}/{suffix}");

    static string Engine(DatabaseEngine engine) => engine switch
    {
        DatabaseEngine.PostgreSql => "postgresql",
        DatabaseEngine.ScyllaDb => "scylladb",
        _ => throw new ArgumentOutOfRangeException(nameof(engine))
    };

    static string Segment(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 160
            || value.Any(static character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'))
            || value is "." or "..")
            throw new ArgumentException("An AWS backup object-key segment is invalid.", name);
        return value;
    }
}

public sealed record AwsMultipartCheckpoint(
    string BucketName,
    string ObjectKey,
    string UploadId,
    int CompletedPartCount,
    long UploadedBytes,
    DateTimeOffset UpdatedUtc);

public interface IAwsMultipartCheckpointStore
{
    ValueTask<AwsMultipartCheckpoint?> ReadAsync(AwsGeneratedObjectKey key, CancellationToken cancellationToken);
    ValueTask WriteAsync(AwsMultipartCheckpoint checkpoint, CancellationToken cancellationToken);
    ValueTask RemoveAsync(AwsGeneratedObjectKey key, CancellationToken cancellationToken);
}
