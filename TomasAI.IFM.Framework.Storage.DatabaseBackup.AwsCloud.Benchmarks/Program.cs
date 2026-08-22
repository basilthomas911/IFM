using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Application.DatabaseBackup.Policies;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Signing;

BenchmarkSwitcher.FromAssembly(typeof(CanonicalManifestBenchmarks).Assembly).Run(args);

[MemoryDiagnoser]
public class CanonicalManifestBenchmarks
{
    readonly DatabaseBackupManifest _manifest;
    readonly byte[] _serialized;

    public CanonicalManifestBenchmarks()
    {
        var operation = new DatabaseRecoveryOperationId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var restorePoint = new DatabaseRestorePointId(operation.Format());
        _manifest = new()
        {
            ManifestId = "benchmark-manifest",
            OperationId = operation,
            RestorePointId = restorePoint,
            Source = BackupSource.AwsCloud,
            Engine = DatabaseEngine.PostgreSql,
            ProtectionSetId = new("core-postgresql"),
            SafeBoundaryReference = "benchmark-boundary",
            CreatedUtc = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero),
            Artifacts = Enumerable.Range(0, 1_000).Select(index => new DatabaseArtifactDigest(
                $"artifacts/{index:D5}.bin", 1_048_576, new string((char)('a' + index % 6), 64))).ToArray(),
            Replicas = [new("primary-vault"), new("recovery-vault")],
            BackupLineage = new()
            {
                RequestedMode = DatabaseBackupMode.Full,
                ResolvedMode = DatabaseBackupMode.Full,
                NativeKind = DatabaseNativeBackupKind.PostgreSqlBase,
                BaseRestorePointId = restorePoint,
                ChainDepth = 0
            }
        };
        _serialized = DatabaseBackupCanonicalJson.Serialize(_manifest);
    }

    [Benchmark(Baseline = true)] public byte[] Serialize() => DatabaseBackupCanonicalJson.Serialize(_manifest);
    [Benchmark] public DatabaseBackupManifest Deserialize() => DatabaseBackupCanonicalJson.Deserialize<DatabaseBackupManifest>(_serialized);
    [Benchmark] public void Validate() => DatabaseBackupManifestPolicy.Validate(_manifest);
}

[MemoryDiagnoser]
public class AwsPublicationDocumentBenchmarks
{
    readonly AwsPublicationRecord _record;
    readonly AwsBackupObjectKeyFactory _keys = new("development");
    readonly DatabaseProtectionSetId _protectionSet = new("postgresql-core");
    readonly DatabaseRestorePointId _restorePoint = new("benchmark-restore-point");

    public AwsPublicationDocumentBenchmarks()
    {
        var operation = new DatabaseRecoveryOperationId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var replica = new DatabaseArtifactReplicaId("aws-primary");
        var version = new AwsImmutableObjectVersion
        {
            BucketName = "ifm-benchmark-primary", Region = "ca-central-1",
            ObjectKey = "v1/environment/development/benchmark", VersionId = "version-1",
            Length = 1_048_576, Sha256 = new string('A', 64), S3ChecksumSha256 = "checksum",
            EncryptionKeyArn = "arn:aws:kms:ca-central-1:107651266250:key/benchmark",
            EncryptionContextBase64 = "e30=", ObjectLockMode = "Governance",
            RetainUntilUtc = new DateTimeOffset(2026, 9, 26, 0, 0, 0, TimeSpan.Zero),
            PublishedUtc = new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero)
        };
        _record = new AwsPublicationRecord
        {
            OperationId = operation, RestorePointId = _restorePoint, ReplicaId = replica,
            ProtectionSetId = _protectionSet, Engine = DatabaseEngine.PostgreSql,
            Artifacts = Enumerable.Range(0, 1_000)
                .Select(index => new AwsPublishedArtifact($"data/{index:D5}.bin", version with
                {
                    ObjectKey = version.ObjectKey + $"/{index:D5}", VersionId = $"version-{index:D5}"
                })).ToArray(),
            EngineManifest = version, EngineManifestSha256 = new string('B', 64),
            EngineManifestSignature = new AwsSignatureEnvelope
            {
                KeyArn = "arn:aws:kms:ca-central-1:107651266250:key/signing", Algorithm = "ECDSA_SHA_256",
                DigestAlgorithm = "SHA-256", DigestBase64 = "digest", SignatureBase64 = "signature",
                SignedUtc = version.PublishedUtc
            },
            ProducingHostId = "benchmark-host", BuildIdentity = "benchmark",
            PublishedUtc = version.PublishedUtc, VerifiedUtc = version.PublishedUtc
        };
    }

    [Benchmark] public byte[] SerializePublication() => DatabaseBackupCanonicalJson.Serialize(_record);

    [Benchmark] public AwsGeneratedObjectKey GenerateArtifactKey()
        => _keys.Artifact(_protectionSet, DatabaseEngine.PostgreSql, _restorePoint,
            new DatabaseArtifactId("artifact-benchmark"), "segment.bin");
}
