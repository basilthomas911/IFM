using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Application.DatabaseBackup.Policies;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Signing;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Observability;
using System.Diagnostics;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;

public sealed class S3DatabaseBackupPublicationCapability(
    IDatabaseNativeArtifactSource nativeArtifacts,
    S3ImmutableObjectStore objects,
    IAwsDocumentSignatureService signatures,
    AwsCloudDatabaseBackupOptions options,
    DatabaseBackupHostOptions hostOptions,
    TimeProvider timeProvider,
    AwsDatabaseBackupTelemetry? telemetry = null) : IDatabaseBackupPublicationCapability
{
    readonly AwsBackupObjectKeyFactory _keys = new(options.Environment.ToString().ToLowerInvariant());
    readonly DatabaseArtifactReplicaId _primaryReplica = new("aws-primary");

    public ValueTask ValidateAsync(DatabaseBackupPublicationPreflightRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        DatabaseBackupEnumValidation.RequireDefined(request.Engine, nameof(request.Engine));
        if (!request.RequiredDestinations.Any(static value => value.Required))
            throw new InvalidOperationException("AWS publication requires at least one required destination.");
        options.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    public async ValueTask<DatabaseBackupPublicationResult> PublishAsync(
        DatabaseBackupPublicationRequest request, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        ValidateRequest(request);
        var restorePoint = new DatabaseRestorePointId(request.OperationId.Format());
        var described = await nativeArtifacts.DescribeAsync(request.Engine, request.OperationId, cancellationToken).ConfigureAwait(false);
        var retainUntil = timeProvider.GetUtcNow().AddDays(options.DefaultRetentionDays);
        var context = EncryptionContext(request, restorePoint);
        var uploaded = new List<AwsPublishedArtifact>(described.Count);
        var manifestArtifacts = new List<DatabaseArtifactDigest>(described.Count);
        foreach (var artifact in described.OrderBy(static value => value.RelativePath, StringComparer.Ordinal))
        {
            var fileName = Path.GetFileName(artifact.RelativePath);
            var artifactId = new DatabaseArtifactId("artifact-" + Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(artifact.RelativePath)))[..12].ToLowerInvariant());
            var key = _keys.Artifact(request.ProtectionSetId, request.Engine, restorePoint, artifactId, fileName);
            var version = await objects.UploadAsync(key,
                token => nativeArtifacts.OpenReadAsync(request.Engine, request.OperationId, artifact.RelativePath, token),
                artifact.Length, retainUntil, context, cancellationToken).ConfigureAwait(false);
            uploaded.Add(new AwsPublishedArtifact(artifact.RelativePath, version));
            manifestArtifacts.Add(new DatabaseArtifactDigest(version.ObjectKey, version.Length, version.Sha256));
        }

        var manifest = CreateManifest(request, restorePoint, [.. manifestArtifacts]);
        DatabaseBackupManifestPolicy.Validate(manifest);
        var manifestBytes = DatabaseBackupCanonicalJson.Serialize(manifest);
        EnsureDocumentBound(manifestBytes);
        var manifestSignature = await signatures.SignAsync(manifestBytes, cancellationToken).ConfigureAwait(false);
        var manifestObject = await UploadDocumentAsync(_keys.EngineManifest(request.ProtectionSetId, request.Engine, restorePoint),
            manifestBytes, retainUntil, context, cancellationToken).ConfigureAwait(false);
        _ = await UploadDocumentAsync(_keys.EngineManifestSignature(request.ProtectionSetId, request.Engine, restorePoint),
            DatabaseBackupCanonicalJson.Serialize(manifestSignature), retainUntil, context, cancellationToken).ConfigureAwait(false);

        var now = timeProvider.GetUtcNow();
        var record = new AwsPublicationRecord
        {
            OperationId = request.OperationId, RestorePointId = restorePoint, ReplicaId = _primaryReplica,
            ProtectionSetId = request.ProtectionSetId,
            Engine = request.Engine, Artifacts = [.. uploaded], EngineManifest = manifestObject,
            EngineManifestSha256 = Convert.ToHexString(SHA256.HashData(manifestBytes)),
            EngineManifestSignature = manifestSignature, Dependencies = request.Dependencies ?? [],
            PostgreSqlTimeline = request.PostgreSqlWalContinuity?.Timeline,
            PostgreSqlStartLsn = request.PostgreSqlWalContinuity?.StartLsn,
            PostgreSqlEndLsn = request.PostgreSqlWalContinuity?.EndLsn,
            ScyllaTopology = request.ScyllaTopology,
            ScyllaSnapshot = request.ScyllaSnapshot,
            ProducingHostId = hostOptions.HostId,
            BuildIdentity = typeof(S3DatabaseBackupPublicationCapability).Assembly.GetName().Version?.ToString() ?? "unknown",
            PublishedUtc = now, VerifiedUtc = now
        };
        var recordBytes = DatabaseBackupCanonicalJson.Serialize(record);
        EnsureDocumentBound(recordBytes);
        var recordSignature = await signatures.SignAsync(recordBytes, cancellationToken).ConfigureAwait(false);
        var recordObject = await UploadDocumentAsync(
            _keys.Publication(request.ProtectionSetId, request.Engine, restorePoint, _primaryReplica),
            recordBytes, retainUntil, context, cancellationToken).ConfigureAwait(false);
        _ = await UploadDocumentAsync(
            _keys.PublicationSignature(request.ProtectionSetId, request.Engine, restorePoint, _primaryReplica),
            DatabaseBackupCanonicalJson.Serialize(recordSignature), retainUntil, context, cancellationToken).ConfigureAwait(false);

        // The catalog entry is deliberately the final write. Nothing is recovery-eligible before this succeeds.
        var catalog = new AwsCatalogEntry
        {
            RestorePointId = restorePoint, ReplicaId = _primaryReplica,
            ProtectionSetId = request.ProtectionSetId, Engine = request.Engine,
            PublicationRecord = recordObject,
            PublicationRecordSha256 = Convert.ToHexString(SHA256.HashData(recordBytes)), PublishedUtc = timeProvider.GetUtcNow()
        };
        _ = await UploadDocumentAsync(_keys.Catalog(restorePoint, _primaryReplica),
            DatabaseBackupCanonicalJson.Serialize(catalog), retainUntil, context, cancellationToken).ConfigureAwait(false);

        var bytes = uploaded.Sum(static value => value.Object.Length);
        telemetry?.RecordUpload(request.Engine, bytes, Stopwatch.GetElapsedTime(started));
        return new DatabaseBackupPublicationResult(restorePoint, manifest.ManifestId, manifest.Revision,
        [
            new DatabaseArtifactReplicaDescriptor
            {
                ArtifactId = new DatabaseArtifactId($"artifact-{request.OperationId.Value:N}"),
                ReplicaId = _primaryReplica, Engine = request.Engine, State = DatabaseArtifactReplicaState.Published,
                SafeDestinationReference = $"{_primaryReplica.Value}:{restorePoint.Value}", Bytes = bytes
            }
        ]);
    }

    async ValueTask<AwsImmutableObjectVersion> UploadDocumentAsync(
        AwsGeneratedObjectKey key, byte[] content, DateTimeOffset retainUntil,
        IReadOnlyDictionary<string, string> context, CancellationToken cancellationToken)
    {
        EnsureDocumentBound(content);
        return await objects.UploadAsync(key,
            _ => ValueTask.FromResult<Stream>(new MemoryStream(content, writable: false)),
            content.LongLength, retainUntil, context, cancellationToken).ConfigureAwait(false);
    }

    DatabaseBackupManifest CreateManifest(
        DatabaseBackupPublicationRequest request, DatabaseRestorePointId restorePoint, DatabaseArtifactDigest[] artifacts)
    {
        var lineage = request.BackupLineage?.NormalizeLegacyFull(request.Engine) ?? new DatabaseBackupLineage
        {
            RequestedMode = DatabaseBackupMode.Full, ResolvedMode = DatabaseBackupMode.Full,
            NativeKind = request.Engine == DatabaseEngine.PostgreSql
                ? DatabaseNativeBackupKind.PostgreSqlBase : DatabaseNativeBackupKind.ScyllaManagerSnapshot,
            BaseRestorePointId = restorePoint
        };
        return new DatabaseBackupManifest
        {
            ManifestId = $"manifest-{request.OperationId.Value:N}", OperationId = request.OperationId,
            RestorePointId = restorePoint, Source = BackupSource.AwsCloud, Engine = request.Engine,
            ProtectionSetId = request.ProtectionSetId, SafeBoundaryReference = request.SafeBoundaryReference,
            CreatedUtc = timeProvider.GetUtcNow(), Dependencies = request.Dependencies ?? [], Artifacts = artifacts,
            Replicas = [_primaryReplica], Statistics = request.Statistics, BackupLineage = lineage
        };
    }

    static Dictionary<string, string> EncryptionContext(DatabaseBackupPublicationRequest request, DatabaseRestorePointId restorePoint)
        => new(StringComparer.Ordinal)
        {
            ["application"] = "IFM", ["component"] = "DatabaseBackup",
            ["operationId"] = request.OperationId.Format(), ["restorePointId"] = restorePoint.Value,
            ["protectionSetId"] = request.ProtectionSetId.Value
        };

    void EnsureDocumentBound(byte[] value)
    {
        if (value.Length > options.MaximumSignedDocumentBytes)
            throw new InvalidDataException("The immutable AWS publication document exceeds its configured bound.");
    }

    static void ValidateRequest(DatabaseBackupPublicationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.OperationId.Value == Guid.Empty || string.IsNullOrWhiteSpace(request.SafeBoundaryReference)
            || request.SafeBoundaryReference.Any(char.IsControl))
            throw new ArgumentException("AWS publication requires an operation and safe native boundary.", nameof(request));
        DatabaseBackupEnumValidation.RequireDefined(request.Engine, nameof(request.Engine));
        if (request.Engine == DatabaseEngine.ScyllaDb)
        {
            AwsScyllaProtectionSetPolicy.ValidatePublication(
                request.Dependencies ?? [], request.BackupLineage, request.ScyllaTopology, request.ScyllaSnapshot);
        }
        else if (request.ScyllaTopology is not null || request.ScyllaSnapshot is not null)
        {
            throw new ArgumentException("PostgreSQL publication cannot contain Scylla evidence.", nameof(request));
        }
    }
}
